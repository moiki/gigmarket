# GigMarket

GigMarket is a small marketplace backend, built as a **learning monorepo** to practice senior-level .NET/Azure architecture: microservices, Clean Architecture, CQRS, distributed communication, testing, and CI/CD.

In the marketplace, **sellers publish "gigs"** (a service they offer, with a price and category) and **buyers place orders** for active gigs. GigMarket is the backend only — a React frontend is planned.

## What's inside

```
gigmarket/
├── services/
│   ├── catalog-api/        # gigs: create, list/filter, get by id, publish (Draft → Active)
│   │   ├── Catalog.Api/            # Minimal API endpoints
│   │   ├── Catalog.Application/    # CQRS commands/queries + MediatR handlers
│   │   ├── Catalog.Domain/         # Gig aggregate + invariants
│   │   ├── Catalog.Infrastructure/ # EF Core + Npgsql
│   │   └── tests/                  # xUnit unit + Testcontainers integration tests
│   └── orders-api/         # orders: create an order for an active gig (idempotent)
│       ├── Orders.Api/
│       ├── Orders.Application/     # CreateOrderCommand + ports (IGigCatalog, IIdempotencyStore, ITransaction)
│       ├── Orders.Domain/          # Order aggregate
│       ├── Orders.Infrastructure/  # EF Core + Npgsql, HttpGigCatalog (sync call to catalog)
│       └── tests/
├── libs/
│   ├── BuildingBlocks.Common/       # Result / Result<T> / Error / ErrorType (no ASP.NET dependency)
│   └── BuildingBlocks.AspNetCore/   # Result → RFC 9457 Problem Details mapping
├── specs/                  # Spec-Driven Development: one markdown spec per feature
├── docker-compose.yml      # PostgreSQL per service (catalog + orders)
└── GigMarket.slnx          # .NET solution
```

### Services and ports (local dev)

| Service | Dev URL | Database | Host port |
|---|---|---|---|
| `catalog-api` | http://localhost:5065 | `gigmarket_catalog` | `5433` |
| `orders-api` | http://localhost:5066 | `gigmarket_orders` | `5434` |

Both APIs are containerized (`services/*/*.Api/Dockerfile`) and can be started together with `docker compose up --build`. Postgres runs on non-default host ports because `5432` is usually taken by a local Postgres install; inside the Compose network the databases use the standard `5432`.

## Tech stack

- **.NET 10** / ASP.NET Core Minimal APIs
- **MediatR** for in-process CQRS (commands/queries + handlers)
- **EF Core 10 + Npgsql** over PostgreSQL (one database per service)
- **Result pattern** (`BuildingBlocks.Common`) instead of exceptions for business failures
- **xUnit** + `WebApplicationFactory` for unit/endpoint tests, **Testcontainers** for integration tests
- **Docker Compose** for local databases

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (or any Docker engine with Compose)

## Quick start

### Option A — run everything with Docker (recommended)

Builds the two APIs and starts them together with their PostgreSQL databases:

```bash
docker compose up --build -d
```

| Container | Host URL | Backed by |
|---|---|---|
| `gigmarket-catalog-api` | http://localhost:5065 | `gigmarket-catalog-postgres` (`5433`) |
| `gigmarket-orders-api` | http://localhost:5066 | `gigmarket-orders-postgres` (`5434`) |

Check status and follow logs:

```bash
docker compose ps
docker compose logs -f orders-api
```

Stop everything (add `-v` to also wipe the database volumes):

```bash
docker compose down
```

Each API applies its EF Core migrations automatically on startup, so the databases are ready on first run.

### Option B — run with `dotnet` (faster dev loop)

Start only the databases in Docker, then run the APIs from the SDK:

```bash
# Databases only
docker compose up -d catalog-postgres orders-postgres

# Terminal 1 — catalog-api  → http://localhost:5065
dotnet run --project services/catalog-api/Catalog.Api

# Terminal 2 — orders-api   → http://localhost:5066 (calls catalog, so start it first)
dotnet run --project services/orders-api/Orders.Api
```

In `Development`, the OpenAPI document is available at `/openapi/v1.json` (e.g. http://localhost:5065/openapi/v1.json).

### Try it

```bash
# Create a gig (starts in Draft)
curl -X POST http://localhost:5065/api/gigs \
  -H 'Content-Type: application/json' \
  -d '{"title":"Clases de guitarra","description":"1 hora","price":25,"category":"Music","ownerId":"11111111-1111-1111-1111-111111111111"}'

# Publish it (Draft → Active)
curl -X POST http://localhost:5065/api/gigs/{gigId}/publish

# Place an order for the active gig (idempotent)
curl -X POST http://localhost:5066/api/orders \
  -H 'Content-Type: application/json' \
  -H 'Idempotency-Key: 9f0b1a8c-1234-4a5b-8c9d-0e1f2a3b4c5d' \
  -d '{"gigId":"{gigId}","buyerId":"22222222-2222-2222-2222-222222222222"}'
```

Retrying the order request with the same `Idempotency-Key` and payload returns the **original** order (`200 OK` + `Idempotency-Replayed: true`) instead of creating a duplicate.

```bash
# Read it back, confirm it, then cancel it
curl http://localhost:5066/api/orders/{orderId}
curl -X POST http://localhost:5066/api/orders/{orderId}/confirm
curl -X POST http://localhost:5066/api/orders/{orderId}/cancel

# List orders (pagination + filters)
curl 'http://localhost:5066/api/orders?page=1&pageSize=20&status=Confirmed'
```

## API overview

**catalog-api**

| Method | Route | Description |
|---|---|---|
| `POST` | `/api/gigs` | Create a gig (Draft) |
| `GET` | `/api/gigs` | List/filter gigs (pagination, status, category, price) |
| `GET` | `/api/gigs/{id}` | Get a gig by id |
| `POST` | `/api/gigs/{id}/publish` | Publish a gig (Draft → Active) |

**orders-api**

| Method | Route | Description |
|---|---|---|
| `POST` | `/api/orders` | Create an order for an active gig (supports `Idempotency-Key`) |
| `GET` | `/api/orders` | List/filter orders (pagination, status, buyerId, providerId) |
| `GET` | `/api/orders/{id}` | Get an order by id |
| `POST` | `/api/orders/{id}/confirm` | Confirm an order (Created → Confirmed) |
| `POST` | `/api/orders/{id}/cancel` | Cancel an order (Created/Confirmed → Cancelled) |

Order state machine:

```
Created --confirm--> Confirmed --cancel--> Cancelled
   \                                          ^
    \---------------cancel--------------------/
```

An invalid transition (e.g. confirming an already `Confirmed` order) returns `409 Order.InvalidStatusTransition`; `Cancelled` is terminal.

Errors follow **RFC 9457 Problem Details**. Business failures map by error type: `Validation → 422`, `NotFound → 404`, `Conflict → 409`, `Unavailable → 503`.

Both APIs expose a **health endpoint** at `GET /health` (used by Docker `HEALTHCHECK` and by Compose to gate startup order). It returns `200 Healthy` when the API can reach its database and `503 Unhealthy` otherwise.

```bash
curl -i http://localhost:5065/health   # catalog-api
curl -i http://localhost:5066/health   # orders-api
```

## Tests

```bash
# Everything (unit + integration)
dotnet test GigMarket.slnx

# Only unit tests (fast, no Docker)
dotnet test services/catalog-api/tests/Catalog.UnitTests/Catalog.UnitTests.csproj
dotnet test services/orders-api/tests/Orders.UnitTests/Orders.UnitTests.csproj
```

Integration tests spin up throwaway PostgreSQL containers via Testcontainers, so **Docker must be running** for those.

## Documentation

- `specs/` — one spec per feature (Spec-Driven Development), e.g. `0006-crear-orden.md`, `0007-idempotencia-orden.md`, `0008-consultar-y-transicionar-orden.md`
- `roadmap-dotnet-senior.md` — the learning roadmap this project follows
- `proyecto-mvp-monorepo.md` — sprint-by-sprint plan

## Roadmap status

Done: monorepo, `catalog-api` (CQRS + Postgres), `orders-api` (sync catalog call + idempotency + read endpoints + state transitions), shared `BuildingBlocks`, health checks, Dockerized APIs + databases.
Next: async communication (Outbox + Service Bus + notifications worker), resilience (Polly), CI/CD and Azure IaC (Bicep).
