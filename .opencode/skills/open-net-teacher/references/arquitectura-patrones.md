# Referencia: Arquitectura y Patrones de Diseño

Úsala cuando el usuario esté trabajando en la estructura de un servicio (`catalog-api`, `orders-api`), diseñando un endpoint, o pidiendo entender un patrón.

## Clean Architecture — capas y reglas de dependencia

```
Domain        ← no depende de nada. Entidades, Value Objects, interfaces de repositorio, Domain Events.
Application   ← depende solo de Domain. Casos de uso (Commands/Queries + Handlers de MediatR).
Infrastructure← depende de Application/Domain. Implementaciones: EF Core, Cosmos SDK, Service Bus client.
Api           ← depende de Application. Controllers/Minimal API endpoints, DI setup, middlewares.
```

**Regla de oro a hacer cumplir:** la flecha de dependencia siempre apunta hacia adentro (hacia Domain). Si ves un `using Infrastructure` dentro de `Domain` o `Application`, es una violación — señálala en el review.

## CQRS + MediatR — patrón de implementación

Estructura típica por caso de uso:

```
Application/
  Gigs/
    Commands/
      CreateGig/
        CreateGigCommand.cs      // record : IRequest<Result<Guid>>
        CreateGigHandler.cs      // : IRequestHandler<CreateGigCommand, Result<Guid>>
        CreateGigValidator.cs    // FluentValidation
    Queries/
      GetGigById/
        GetGigByIdQuery.cs
        GetGigByIdHandler.cs
```

Preguntas que debes hacerle al usuario para reforzar el "por qué":
- "¿Por qué separamos Command de Query aquí en vez de un solo `GigService` con todos los métodos?" → respuesta esperada: aislamiento de responsabilidades, permite optimizar lecturas y escrituras independientemente (ej. leer de una réplica o de una proyección distinta).
- "¿MediatR es obligatorio para hacer CQRS?" → no, es solo una herramienta de despacho; el patrón es la separación conceptual, MediatR es implementación.

Pipeline behaviors de MediatR a introducir progresivamente: `ValidationBehavior`, `LoggingBehavior`, `TransactionBehavior` — buen ejemplo de Decorator/Chain of Responsibility en código real.

## Result Pattern (evitar excepciones como control de flujo)

```csharp
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    // Success/Failure factory methods
}
```

Enséñalo comparando: lanzar `NotFoundException` desde el dominio y capturarla en un middleware global VS devolver `Result<T>.Failure("Gig not found")` y mapear a `404` en la capa API. Ambos son válidos; lo importante es que el usuario elija uno y sea consistente en todo el proyecto — inconsistencia es la señal de "no senior" en un review.

## Repository + Unit of Work — cuándo SÍ y cuándo NO

- **Con EF Core**, `DbContext` ya ES un Unit of Work y `DbSet<T>` ya es casi un Repository. Un Repository extra sobre EF Core a veces es una capa de indirección sin valor.
- **Úsalo cuando**: quieres aislar el dominio de EF Core por completo (testear sin base de datos, o soportar múltiples fuentes de datos), o cuando trabajas con Cosmos DB SDK directo (ahí sí aporta mucho, porque el SDK de Cosmos es más verboso).
- Pídele al usuario que justifique en la spec/ADR por qué sí o por qué no lo usa en cada servicio.

## Diseño de API REST — checklist de revisión

- Verbos y rutas: `POST /gigs`, `GET /gigs/{id}`, no `POST /createGig`
- Paginación: `?page=1&pageSize=20` con metadata de total en el response o headers
- Errores: usar **Problem Details (RFC 7807)** consistente en todos los endpoints
- Versionado: `/v1/gigs` o header — decidir uno desde el inicio
- Idempotencia en POST críticos (ej. crear orden): soportar `Idempotency-Key` header

## Domain-Driven Design ligero (lo mínimo útil para este proyecto)

- **Entidad**: tiene identidad (`GigId`), su igualdad es por Id.
- **Value Object**: sin identidad, igualdad por valor (ej. `Money`, `Price`).
- **Aggregate Root**: la única entrada válida para modificar un grupo de entidades relacionadas (ej. `Order` es el aggregate root de `OrderItems`).
- **Domain Event**: algo que pasó en el dominio (`GigCreatedDomainEvent`) — se dispara desde la entidad y se despacha después de guardar (buen punto para introducir el Outbox Pattern, ver `comunicacion-distribuida.md`).

No sobre-diseñes DDD táctico completo (Specifications, Domain Services complejos) si el usuario apenas está en Fase 1 — es más valioso que entienda bien Aggregate + Domain Event primero.
