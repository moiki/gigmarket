using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using BuildingBlocks.AspNetCore;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Orders.Api;
using Orders.Application;
using Orders.Domain;
using Orders.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var connectionString = builder.Configuration.GetConnectionString("GigmarketOrders")
    ?? throw new InvalidOperationException("La cadena de conexión 'GigmarketOrders' es obligatoria.");

builder.Services.AddDbContext<OrderDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();
builder.Services.AddScoped<IIdempotencyStore, EfIdempotencyStore>();
builder.Services.AddScoped<ITransaction, EfTransaction>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateOrderCommandHandler).Assembly));
builder.Services.AddHealthChecks().AddDbContextCheck<OrderDbContext>("database");

builder.Services.AddHttpClient<IGigCatalog, HttpGigCatalog>(client =>
{
    var baseUrl = builder.Configuration["Services:Catalog:BaseUrl"]
        ?? throw new InvalidOperationException("'Services:Catalog:BaseUrl' es obligatorio.");
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", defaultValue: true))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    dbContext.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health");

app.MapPost("/api/orders", async (CreateOrderRequest request, HttpRequest httpRequest, HttpResponse httpResponse, ISender mediator, IOrderRepository orders, IIdempotencyStore idempotency, ITransaction transaction, TimeProvider clock, CancellationToken ct) =>
    {
        var hasKey = httpRequest.Headers.TryGetValue("Idempotency-Key", out var rawKey);
        var key = rawKey.ToString().Trim();

        if (hasKey && (key.Length == 0 || key.Length > 128))
            return TypedResults.Problem(
                detail: OrderErrors.InvalidIdempotencyKey.Message,
                title: OrderErrors.InvalidIdempotencyKey.Code,
                statusCode: StatusCodes.Status400BadRequest);

        var requestHash = hasKey ? HashPayload(request.GigId, request.BuyerId) : null;

        if (hasKey)
        {
            var existing = idempotency.TryGet(request.BuyerId, key);

            if (existing is not null)
            {
                if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                    return OrderErrors.IdempotencyKeyMismatch.ToProblem();

                return Replay(existing.OrderId, orders, httpResponse);
            }
        }

        await transaction.BeginAsync(ct);
        var result = await mediator.Send(new CreateOrderCommand(request.GigId, request.BuyerId), ct);

        if (result.IsFailure)
        {
            await transaction.RollbackAsync(ct);
            return result.Error.Value.ToProblem();
        }

        if (hasKey)
        {
            try
            {
                idempotency.Record(request.BuyerId, key, requestHash!, result.Value.Id, clock.GetUtcNow().UtcDateTime);
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(ct);

                var raced = idempotency.TryGet(request.BuyerId, key);
                if (raced is not null && string.Equals(raced.RequestHash, requestHash, StringComparison.Ordinal))
                    return Replay(raced.OrderId, orders, httpResponse);

                return OrderErrors.IdempotencyKeyMismatch.ToProblem();
            }
        }

        await transaction.CommitAsync(ct);

        var order = result.Value;
        return Results.Created($"/api/orders/{order.Id}", OrderResponse.From(order));
    })
    .WithName("CreateOrder")
    .Produces<OrderResponse>(StatusCodes.Status201Created)
    .Produces<OrderResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.MapGet("/api/orders/{id:guid}", async (Guid id, ISender mediator, CancellationToken ct) =>
    {
        var order = await mediator.Send(new GetOrderByIdQuery(id), ct);

        return order is null
            ? TypedResults.Problem(
                detail: OrderErrors.NotFound.Message,
                title: OrderErrors.NotFound.Code,
                statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(OrderResponse.From(order));
    })
    .WithName("GetOrder")
    .Produces<OrderResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status404NotFound);

app.MapGet("/api/orders", async (int? page, int? pageSize, string? status, Guid? buyerId, Guid? providerId, ISender mediator, CancellationToken ct) =>
    {
        var actualPage = page ?? 1;
        var actualPageSize = pageSize ?? 20;

        if (actualPage < 1)
            return InvalidQuery("El parámetro 'page' debe ser mayor o igual a 1.");
        if (actualPageSize is < 1 or > 100)
            return InvalidQuery("El parámetro 'pageSize' debe estar entre 1 y 100.");
        if (!TryParseStatus(status, out var actualStatus))
            return InvalidQuery("El parámetro 'status' no es un estado válido.");

        var result = await mediator.Send(new GetOrdersQuery(actualPage, actualPageSize, actualStatus, buyerId, providerId), ct);

        return Results.Ok(new GetOrdersResponse(
            result.Items.Select(OrderResponse.From).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages));
    })
    .WithName("GetOrders")
    .Produces<GetOrdersResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status400BadRequest);

app.MapPost("/api/orders/{id}/confirm", async (Guid id, ISender mediator, CancellationToken ct) =>
    {
        var result = await mediator.Send(new ConfirmOrderCommand(id), ct);

        if (result.IsFailure)
            return result.Error.Value.ToProblem();

        return Results.Ok(OrderResponse.From(result.Value));
    })
    .WithName("ConfirmOrder")
    .Produces<OrderResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict);

app.MapPost("/api/orders/{id}/cancel", async (Guid id, ISender mediator, CancellationToken ct) =>
    {
        var result = await mediator.Send(new CancelOrderCommand(id), ct);

        if (result.IsFailure)
            return result.Error.Value.ToProblem();

        return Results.Ok(OrderResponse.From(result.Value));
    })
    .WithName("CancelOrder")
    .Produces<OrderResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict);

app.Run();

static IResult InvalidQuery(string detail) =>
    TypedResults.Problem(detail: detail, title: "InvalidQueryParameters", statusCode: StatusCodes.Status400BadRequest);

static bool TryParseStatus(string? raw, out OrderStatus? status)
{
    status = null;

    if (raw is null)
        return true;

    if (Enum.TryParse<OrderStatus>(raw, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
    {
        status = parsed;
        return true;
    }

    return false;
}

static IResult Replay(Guid orderId, IOrderRepository orders, HttpResponse response)
{
    var order = orders.GetById(orderId);

    if (order is null)
        return OrderErrors.ReplayUnavailable.ToProblem();

    response.Headers["Idempotency-Replayed"] = "true";
    response.Headers["Location"] = $"/api/orders/{order.Id}";

    return Results.Ok(OrderResponse.From(order));
}

static string HashPayload(Guid gigId, Guid buyerId)
{
    var bytes = Encoding.UTF8.GetBytes($"{gigId:N}{buyerId:N}");
    return Convert.ToHexString(SHA256.HashData(bytes));
}

public partial class Program;