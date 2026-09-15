using System.Text.Json.Serialization;
using Catalog.Api;
using Catalog.Application;
using Catalog.Domain;
using Catalog.Infrastructure;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton<IGigRepository, InMemoryGigRepository>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateGigCommandHandler).Assembly));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/api/gigs", async (CreateGigRequest request, ISender mediator, CancellationToken ct) =>
    {
        var result = await mediator.Send(new CreateGigCommand(
            request.Title,
            request.Description,
            request.Price,
            request.Category,
            request.OwnerId), ct);

        if (!result.IsSuccess)
        {
            return Results.Problem(
                detail: result.Error.Message,
                title: result.Error.Code,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var gig = result.Value;
        return Results.Created($"/api/gigs/{gig.Id}", GigResponse.From(gig));
    })
    .WithName("CreateGig")
    .Produces<GigResponse>(StatusCodes.Status201Created)
    .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

app.MapGet("/api/gigs", async (int? page, int? pageSize, string? status, ISender mediator, CancellationToken ct) =>
    {
        var actualPage = page ?? 1;
        var actualPageSize = pageSize ?? 20;

        if (actualPage < 1)
            return InvalidQuery("El parámetro 'page' debe ser mayor o igual a 1.");
        if (actualPageSize is < 1 or > 100)
            return InvalidQuery("El parámetro 'pageSize' debe estar entre 1 y 100.");

        if (!TryParseStatus(status, out var actualStatus))
            return InvalidQuery("El parámetro 'status' no es un estado válido.");

        var result = await mediator.Send(new GetGigsQuery(actualPage, actualPageSize, actualStatus), ct);

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

app.MapPost("/api/gigs/{id}/publish", async (Guid id, ISender mediator, CancellationToken ct) =>
    {
        var result = await mediator.Send(new PublishGigCommand(id), ct);

        if (!result.IsSuccess)
        {
            var statusCode = result.Error.Code == GigErrors.GigNotFound.Code
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status422UnprocessableEntity;

            return Results.Problem(
                detail: result.Error.Message,
                title: result.Error.Code,
                statusCode: statusCode);
        }

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

public partial class Program;