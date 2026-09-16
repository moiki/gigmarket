using System.Text.Json.Serialization;
using BuildingBlocks.AspNetCore;
using Catalog.Api;
using Catalog.Application;
using Catalog.Domain;
using Catalog.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var connectionString = builder.Configuration.GetConnectionString("GigmarketCatalog")
    ?? throw new InvalidOperationException("La cadena de conexión 'GigmarketCatalog' es obligatoria.");

builder.Services.AddDbContext<GigDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IGigRepository, EfGigRepository>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateGigCommandHandler).Assembly));
builder.Services.AddHealthChecks().AddDbContextCheck<GigDbContext>("database");

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", defaultValue: true))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<GigDbContext>();
    dbContext.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health");

app.MapPost("/api/gigs", async (CreateGigRequest request, ISender mediator, CancellationToken ct) =>
    {
        var result = await mediator.Send(new CreateGigCommand(
            request.Title,
            request.Description,
            request.Price,
            request.Category,
            request.OwnerId), ct);

        if (result.IsFailure)
            return result.Error.Value.ToProblem();

        var gig = result.Value;
        return Results.Created($"/api/gigs/{gig.Id}", GigResponse.From(gig));
    })
    .WithName("CreateGig")
    .Produces<GigResponse>(StatusCodes.Status201Created)
    .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

app.MapGet("/api/gigs", async (int? page, int? pageSize, string? status, string? category, decimal? minPrice, decimal? maxPrice, ISender mediator, CancellationToken ct) =>
    {
        var actualPage = page ?? 1;
        var actualPageSize = pageSize ?? 20;

        if (actualPage < 1)
            return InvalidQuery("El parámetro 'page' debe ser mayor o igual a 1.");
        if (actualPageSize is < 1 or > 100)
            return InvalidQuery("El parámetro 'pageSize' debe estar entre 1 y 100.");

        if (!TryParseStatus(status, out var actualStatus))
            return InvalidQuery("El parámetro 'status' no es un estado válido.");
        if (!TryParseCategory(category, out var actualCategory))
            return InvalidQuery("El parámetro 'category' no es una categoría válida.");
        if (minPrice is < 0)
            return InvalidQuery("El parámetro 'minPrice' no puede ser negativo.");
        if (maxPrice is < 0)
            return InvalidQuery("El parámetro 'maxPrice' no puede ser negativo.");
        if (minPrice.HasValue && maxPrice.HasValue && minPrice > maxPrice)
            return InvalidQuery("El parámetro 'minPrice' no puede ser mayor que 'maxPrice'.");

        var result = await mediator.Send(new GetGigsQuery(actualPage, actualPageSize, actualStatus, actualCategory, minPrice, maxPrice), ct);

        return Results.Ok(new GetGigsResponse(
            result.Items.Select(GigResponse.From).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages));
    })
    .WithName("GetGigs")
    .Produces<GetGigsResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status400BadRequest);

app.MapGet("/api/gigs/{id:guid}", async (Guid id, ISender mediator, CancellationToken ct) =>
    {
        var gig = await mediator.Send(new GetGigByIdQuery(id), ct);

        return gig is null
            ? TypedResults.Problem(
                detail: GigErrors.GigNotFound.Message,
                title: GigErrors.GigNotFound.Code,
                statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(GigResponse.From(gig));
    })
    .WithName("GetGig")
    .Produces<GigResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status404NotFound);

app.MapPost("/api/gigs/{id}/publish", async (Guid id, ISender mediator, CancellationToken ct) =>
    {
        var result = await mediator.Send(new PublishGigCommand(id), ct);

        if (result.IsFailure)
            return result.ToHttpResult();

        return Results.Ok(GigResponse.From(result.Value));
    })
    .WithName("PublishGig")
    .Produces<GigResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

app.Run();

static IResult InvalidQuery(string detail) =>
    TypedResults.Problem(detail: detail, title: "InvalidQueryParameters", statusCode: StatusCodes.Status400BadRequest);

static bool TryParseStatus(string? raw, out GigStatus? status)
{
    status = null;

    if (raw is null)
        return true;

    if (Enum.TryParse<GigStatus>(raw, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
    {
        status = parsed;
        return true;
    }

    return false;
}

static bool TryParseCategory(string? raw, out GigCategory? category)
{
    category = null;

    if (raw is null)
        return true;

    if (Enum.TryParse<GigCategory>(raw, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
    {
        category = parsed;
        return true;
    }

    return false;
}

public partial class Program;