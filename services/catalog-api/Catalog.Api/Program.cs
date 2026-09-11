using System.Text.Json.Serialization;
using Catalog.Api;
using Catalog.Application;
using Catalog.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton<IGigRepository, InMemoryGigRepository>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<CreateGigService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/api/gigs", (CreateGigRequest request, CreateGigService service) =>
    {
        var result = service.Create(new CreateGigCommand(
            request.Title,
            request.Description,
            request.Price,
            request.Category,
            request.OwnerId));

        if (!result.IsSuccess)
        {
            return Results.Problem(
                detail: result.Error.Message,
                title: result.Error.Code,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var gig = result.Value;
        return Results.Created($"/api/gigs/{gig.Id}", new CreateGigResponse(
            gig.Id,
            gig.Title,
            gig.Description,
            gig.Price,
            gig.Category,
            gig.Status,
            gig.OwnerId,
            gig.CreatedAt));
    })
    .WithName("CreateGig")
    .Produces<CreateGigResponse>(StatusCodes.Status201Created)
    .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

app.Run();

public partial class Program;