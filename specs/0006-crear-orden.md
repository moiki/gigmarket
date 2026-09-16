# 0006 - Crear Orden (orders-api)

## Problema
Un buyer reserva/contrata un gig activo y eso genera una **orden**. Es el primer endpoint de `orders-api`, el segundo servicio del monorepo, y el primer caso de comunicación síncrona entre servicios (orders → catalog).

## Contrato de API

### Dependencia previa: `GET /api/gigs/{id}` en catalog-api
orders-api valida el gig consultando el catálogo, pero catalog-api **no tiene** detalle por id. Se agrega:
- **Endpoint:** `GET /api/gigs/{id}`
- **200 OK:** `GigResponse` (existente, incluye `id`, `price`, `ownerId`, `status`, ...)
- **404 Problem Details:** si el gig no existe.
- Sirve también para el frontend (ver detalle de un gig). Sin transiciones de estado.

### Nuevo endpoint `POST /api/orders`
- **Request:**
  ```json
  {
    "gigId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "buyerId": "9f0b1a8c-1234-4a5b-8c9d-0e1f2a3b4c5d"
  }
  ```
- **Response 201 Created:**
  ```json
  {
    "id": "b0b0c1d2-...",
    "gigId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "buyerId": "9f0b1a8c-1234-4a5b-8c9d-0e1f2a3b4c5d",
    "providerId": "1a2b3c4d-...",
    "price": 25.0,
    "status": "Created",
    "createdAt": "2026-09-16T10:00:00Z"
  }
  ```
  Con header `Location: /api/orders/{id}`.
- **Errores (Problem Details, RFC 9457):**
  - `422 Validation` — `buyerId` o `gigId` no son Guid no vacíos.
  - `404 NotFound` — el gig no existe en catalog.
  - `409 Conflict` — el gig existe pero no está `Active`.
  - `503 Service Unavailable` — catalog-api no responde (upstream caído/timeout).

## Reglas de negocio / invariantes (se validan al construir el `Order`, nunca estado inválido en memoria)
1. `buyerId` es un `Guid` no vacío (aún no hay servicio de usuarios).
2. `gigId` es un `Guid` no vacío.
3. La orden nace en estado `Created`.
4. El precio se **toma del gig** (catalog), nunca del request: `0 < price <= 100000`.
5. `providerId` = `ownerId` del gig (denormalizado en la orden como snapshot).
6. Sólo se puede crear una orden contra un gig **existente y `Active`**.

## Casos borde
- `buyerId` Guid zero → 422
- `gigId` Guid zero → 422
- Gig inexistente en catalog → 404
- Gig en `Draft` (no activo) → 409
- Catalog caído (HttpClient timeout, conexión rechazada) → 503, sin crear orden

## Criterios de aceptación (testeable)
- [ ] Dado un gig activo, cuando se hace `POST /api/orders`, entonces responde `201`, la orden nace `Created`, `price` = precio del gig y `providerId` = `ownerId` del gig.
- [ ] Dado un `buyerId` Guid zero, cuando se hace `POST /api/orders`, entonces responde `422` y **no** se crea la orden.
- [ ] Dado un `gigId` inexistente en catalog, cuando se hace `POST /api/orders`, entonces responde `404` y **no** se crea la orden.
- [ ] Dado un gig en `Draft`, cuando se hace `POST /api/orders`, entonces responde `409` y **no** se crea la orden.
- [ ] Dado catalog-api caído, cuando se hace `POST /api/orders`, entonces responde `503` y **no** se crea la orden.
- [ ] Dado una orden creada con éxito, el repositorio puede devolverla (prepara `GET /api/orders/{id}`).

## Decisión de arquitectura
- `orders-api` replica la estructura Clean Arch de catalog: `Orders.Domain`, `Orders.Application` (CQRS + MediatR), `Orders.Infrastructure`, `Orders.Api` (Minimal API).
- **Reuso de BuildingBlocks:** `BuildingBlocks.Common` (`Result`/`Result<T>`/`Error`/`ErrorType`) + `BuildingBlocks.AspNetCore` (`ToHttpResult`/`ToProblem`) — el refactor del Result pattern se capitaliza aquí; no se duplica mapeo HTTP.
- **Validación síncrona del gig:** puerto `IGigCatalog` en Application; `HttpGigCatalog` (HttpClient tipado) en Infrastructure. Respeta Dependency Inversion — el contrato de catalog es un detalle de infraestructura intercambiable.
- **Persistencia:** EF Core + Npgsql sobre Postgres (consistencia con catalog en local; desviación del roadmap que decía Azure SQL — ver ADR pendiente en sprint 6). Migración EF Core desde el día 1.
- **Transacciones:** EF Core `Add` + `SaveChanges` por request; sin UnitOfWork explícito (los handlers son la unidad transaccional). Sin Outbox aún (Sprint 4).
- **Catalog GET detail:** necesario porque orders no puede confiar en un `gigId` no verificado.

## Fuera de alcance
- `GET /api/orders` (listar) y `GET /api/orders/{id}` — siguiente sprint
- Transiciones `Confirmed` / `Cancelled` (evento para notificaciones llega en Sprint 4)
- Pagos reales (el MVP no tiene servicio de pagos; el precio es snapshot informativo)
- Resiliencia (Polly/retry/timeout configurable) — en esta spec solo el 503 básico; se endurece en Sprint 5
- **Idempotencia** (risk alto: un retry del cliente crearía órdenes duplicadas; agendar para 0007)
- Outbox / Service Bus / notificaciones
- Autenticación / autorización / identidad del buyer real
- Multi-moneda (precio único, sin `currency`)