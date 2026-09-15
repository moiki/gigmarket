---
name: dotnet-cqrs
description: >
  Estándares del proyecto GigMarket para crear Commands/Queries CQRS con
  MediatR y Clean Architecture (.NET). Define nombres, interfaces, política de
  retorno (Result<T> vs datos), dónde vive cada validación, y los tests
  esperados. Trigger: al implementar una feature nueva de catálogo/órdenes que
  escriba o modifique estado (Command), o que lea datos (Query), al crear los
  archivos de Command/Query + Handler en Catalog.Application, o al pedir
  "crear command", "crear query", "CQRS".
license: Apache-2.0
metadata:
  author: gentleman-programming
  version: "1.0"
---

## When to Use

- Nueva feature de negocio que escribe (crear, publicar, editar, reservar) → **Command**.
- Nueva feature que lee (listar, obtener detalle) → **Query**.
- Antes de escribir código: la feature **debe** tener spec aprobada en `specs/NNNN-nombre.md` (regla del proyecto, ver skill `open-net-teacher`).

## Critical Patterns

### 1. Clasificación (CQRS)
| Operación en la API | Naturaleza | Artefacto |
|---|---|---|
| POST/PUT/PATCH/DELETE | Command (cambia estado) | `XxxCommand` + `XxxCommandHandler` |
| GET | Query (solo lee) | `XxxQuery` + `XxxQueryHandler` |

Si la operación no cambia estado y no es lectura (ej. trigger), replantea el caso: en este proyecto GET debe ser Query y el resto Command.

### 2. Ubicación y nombres
- Commands/Queries + handlers viven en **`Catalog.Application`** (raíz, plana por ahora). Si en `Application` superan ~10 archivos, introducir subcarpetas `Commands/` y `Queries/`.
- Nombres **verbo + sustantivo**: `CreateGigCommand`, `CreateGigCommandHandler`, `GetGigsQuery`, `GetGigsQueryHandler`.
- No usar sufijo `Service` para handlers (se eliminó `CreateGigService`).

### 3. Interfaces MediatR
- Command: `public sealed record XxxCommand(...) : IRequest<Result<T>>;`
- Query: `public sealed record XxxQuery(...) : IRequest<SomeType>;`
- Handler: `: IRequestHandler<XxxCommand, Result<T>>` / `IRequestHandler<XxxQuery, SomeType>`.
- Inyectar deps en constructor primario: `IRequestHandler<..>(IGigRepository gigs, TimeProvider clock)`.

### 4. Política de retorno — obligatorio
| Caso | Retorna | Motivo |
|---|---|---|
| Command | `Result<T>` | Falla de negocio = resultado esperado, no excepción |
| Query | El dato directo (ej. `Gig?`, `PagedResult<T>`) | Lectura con params válidos no falla; envolver en Result añade ruido |

- Nunca excepciones de dominio para control de flujo (excepto programación: usar excepción solo si es un bug, ej. estado ilegal).
- Commands aplican al repo dentro del handler; un Command que falla **no** debe persistir nada.

### 5. Dónde valida cada cosa (no mezclar capas)
| Naturaleza de la validación | Vive en | Status HTTP |
|---|---|---|
| Invariantes de negocio (precio, título, estado) | `Catalog.Domain` (static factory `Create` / método que devuelve `Result`, p. ej. `Publish()`) | `422` |
| Parámetros del request HTTP (page, pageSize, enum inexistente) | `Catalog.Api` (endpoint) | `400` |
| Shape/proyección hacia afuera | `Catalog.Api` (DTOs + `GigResponse.From(Gig)`) | — |

- Prohibido: reglas de negocio en el endpoint, validación de HTTP en Domain, o `Result<T>` expuesto en el shape de respuesta.

### 6. Endpoint (Minimal API)
- Usar `ISender` (no `IMediator`) y pasar `CancellationToken`.
- Mapear `Result<T>` → status (`.IsSuccess` → 2xx con `GigResponse.From(...)`; `!IsSuccess` → `Results.Problem` con `Error.Code` como `title` y `Error.Message` como `detail`).
- Anotar `.WithName(...)` + `.Produces<T>(status)` + `.ProducesProblem(status)`.
- Query params inválidos → `TypedResults.Problem(..., statusCode: 400)`. No crear DTOs de request para GET (se bindan como argumentos: `int? page, int? pageSize, string? status`).

### 7. DI
- Registrar una sola vez: `builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateGigCommandHandler).Assembly));`.
- No registrar handlers uno por uno.

### 8. Tests — obligatorios
- **Handler (unit)**: en `services/catalog-api/tests/Catalog.UnitTests`, xUnit con `Assert` plano, métodos `async Task` con `await` (nunca `.Result` — xUnit1031).
- **Endpoint (contracto HTTP)**: con `WebApplicationFactory<Program>`; seedear datos vía `factory.Services.GetRequiredService<IGigRepository>()`.
- **Transiciones internas de dominio**: exponer método `internal` + `InternalsVisibleTo` en el `.csproj` del Domain hacia `Catalog.UnitTests` (ej. `Gig.Publish()`), en vez de reflection o setters públicos.
- Cada criterio de aceptación de la spec debe tener al menos un test que lo cubra (no solo el happy path).

## Code Examples

Command + Handler (Application):

```csharp
public sealed record CreateGigCommand(
    string Title, string? Description, decimal Price,
    string Category, Guid OwnerId) : IRequest<Result<Gig>>;

public sealed class CreateGigCommandHandler(IGigRepository gigs, TimeProvider clock)
    : IRequestHandler<CreateGigCommand, Result<Gig>>
{
    public Task<Result<Gig>> Handle(CreateGigCommand command, CancellationToken ct)
    {
        var result = Gig.Create(
            Guid.NewGuid(), command.Title, command.Description,
            command.Price, command.Category, command.OwnerId,
            clock.GetUtcNow().UtcDateTime);

        if (result.IsSuccess)
            gigs.Add(result.Value);

        return Task.FromResult(result);
    }
}
```

Query con paginación (Application):

```csharp
public sealed record GetGigsQuery(int Page, int PageSize, GigStatus? Status)
    : IRequest<PagedResult<Gig>>;

public sealed class GetGigsQueryHandler(IGigRepository gigs)
    : IRequestHandler<GetGigsQuery, PagedResult<Gig>>
{
    public Task<PagedResult<Gig>> Handle(GetGigsQuery query, CancellationToken ct)
    {
        var status = query.Status ?? GigStatus.Active;
        var matching = gigs.GetAll()
            .Where(g => g.Status == status)
            .OrderByDescending(g => g.CreatedAt)
            .ToList();

        var items = matching
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var totalPages = (int)Math.Ceiling(matching.Count / (double)query.PageSize);

        return Task.FromResult(new PagedResult<Gig>(items, query.Page, query.PageSize, matching.Count, totalPages));
    }
}
```

Endpoint (API):

```csharp
app.MapPost("/api/gigs", async (CreateGigRequest request, ISender mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new CreateGigCommand(
        request.Title, request.Description, request.Price, request.Category, request.OwnerId), ct);

    return result.IsSuccess
        ? Results.Created($"/api/gigs/{result.Value.Id}", GigResponse.From(result.Value))
        : Results.Problem(detail: result.Error.Message, title: result.Error.Code,
            statusCode: StatusCodes.Status422UnprocessableEntity);
})
.WithName("CreateGig")
.Produces<GigResponse>(StatusCodes.Status201Created)
.ProducesProblem(StatusCodes.Status422UnprocessableEntity);
```

Test de handler (unit):

```csharp
[Fact]
public async Task Handle_WithValidCommand_ReturnsSuccessAndStoresGig()
{
    var handler = new CreateGigCommandHandler(new InMemoryGigRepository(), new FakeTimeProvider(Now));

    var result = await handler.Handle(new CreateGigCommand("Clases de guitarra", null, 25m, "Music", OwnerId), CancellationToken.None);

    Assert.True(result.IsSuccess);
    Assert.Equal(GigStatus.Draft, result.Value.Status);
}
```

## Commands

```bash
# Build + tests del monorepo
dotnet build GigMarket.slnx
dotnet test GigMarket.slnx

# Build solo catalog
dotnet build services/catalog-api/Catalog.Api/Catalog.Api.csproj
```

## Resources

- **Proyecto**: `proyecto-mvp-monorepo.md`, `roadmap-dotnet-senior.md`
- **Specs**: `specs/0001-crear-gig.md`, `specs/0002-obtener-gigs.md`
- **Referencia de patrones**: skill `open-net-teacher` → `references/arquitectura-patrones.md` (Clean Architecture, CQRS, Result Pattern)