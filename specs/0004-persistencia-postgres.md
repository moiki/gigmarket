# 0004 - Persistencia real del catálogo (PostgreSQL + EF Core)

## Problema
El catálogo vive en `InMemoryGigRepository`: todo se pierde al reiniciar el servicio. Se reemplaza por persistencia real en **PostgreSQL + EF Core**, sin tocar `Catalog.Domain` ni `Catalog.Application` (el puerto `IGigRepository` de la spec 0001 es la frontera que hace esto posible; el futuro swap a Cosmos DB o el `orders-api` con Azure SQL también pasarán por aquí).

## Contrato de API
- **Sin cambios en la API pública.** Los shapes de `POST /api/gigs`, `GET /api/gigs` y `POST /api/gigs/{id}/publish` (specs 0001, 0002, 0003) quedan iguales.
- `GET /api/gigs` mantiene el contrato de spec 0002, pero ahora el filtrado/orden/paginación se ejecuta en SQL (no en memoria).

## Reglas de negocio / invariantes
- No cambia ninguna regla de negocio: esta spec es de infraestructura.
- Los datos persisten entre reinicios del servicio.
- El estado `Active`/`Draft` persistido es la fuente de verdad para el listado público.

## Casos borde
- DB caída o sin conexión → el servicio falla rápido al arrancar (fail fast), no degrada silenciosamente.
- `Add` de un gig con `Id` ya existente → es un error de integridad (PK), no se sobreescribe.
- La regla `Draft → Active` sigue validándose en dominio antes de persistir; no hay transición inválida que llegue a la DB.
- Concurrencia real (dos publicaciones simultáneas) → fuera de alcance (ver "Fuera de alcance": sin concurrency tokens por ahora).

## Criterios de aceptación (testeable)
- [ ] Dado un gig creado vía `POST /api/gigs`, cuando el servicio se reinicia, entonces el gig persiste y aparece en `GET /api/gigs`.
- [ ] Dado un catálogo con N gigs persistidos, cuando se hace `GET /api/gigs` con `page`/`pageSize`/`status`, entonces el filtrado, orden y paginación los ejecuta la base de datos (mismo contrato de spec 0002).
- [ ] Dado un gig publicado (spec 0003), cuando se consulta `GET /api/gigs`, entonces aparece con estado `Active` persistido.
- [ ] Dado un cambio de esquema futuro, entonces se aplica vía una nueva migración EF versionada y commiteada (nunca `EnsureCreated`).
- [ ] Dado el repo EF real, cuando se corren los tests de integración, entonces se ejecutan contra una base PostgreSQL real vía Testcontainers (no contra el repo en memoria).

## Decisión de arquitectura
- **`Catalog.Infrastructure`** gana `GigDbContext` (EF Core + `Npgsql.EntityFrameworkCore.PostgreSQL`) y `EfGigRepository : IGigRepository`.
- **`IGigRepository` se redefine** — `Add`, `GetById`, y un nuevo `GetGigs(status, page, pageSize) → PagedResult<Gig>` que traduce a SQL (`WHERE status = @p ORDER BY created_at DESC OFFSET ... LIMIT ...` + `COUNT`). **Se retira `GetAll()`**: era el vector de "traer todo a memoria", inaceptable con DB real. Este era el punto diferido de la spec 0002 ("se decide en implementation").
- El handler `GetGigsQueryHandler` se adelgaza: pasa el `status ?? Active` y delega todo al repo.
- **Mapeo de `Gig`:** constructor privado + propiedades con setter privado (EF Core los materializa vía constructor binding y setters); `GigStatus`/`GigCategory` como string en la DB (legible) — pendiente de decidir int vs string en implementation; `decimal` con precisión configurada.
- **Dobles de test:** `InMemoryGigRepository` se conserva en Infrastructure como doble para tests unitarios (handlers) y endpoint tests. Los tests del `EfGigRepository` viven en un proyecto nuevo `Catalog.IntegrationTests` con **Testcontainers** (`Npgsql` container).
- **Endpoint tests:** `WebApplicationFactory.WithWebHostBuilder(...)` reemplaza el `EfGigRepository` registrado por el in-memory (fást, aislado).
- **Program.cs:** registra `EfGigRepository` + `GigDbContext` (conexión desde `appsettings`).
- **Migraciones:** versionadas y commiteadas en `Catalog.Infrastructure/Migrations/`, generadas con `dotnet-ef`.
- **Local:** `docker-compose.yml` en la raíz con `postgres:17-alpine` (dev).

## Fuera de alcance
- Cosmos DB (el salto futuro aprovecha el puerto; no se toca ahora).
- Concurrency tokens / optimistic concurrency — futura spec.
- Proyecciones de lectura separadas (CQRS read models diferenciados) — el listado sigue materializando `Gig`.
- Búsqueda full-text, filtros por categoría/precio adicionales.
- Dapper como alternativa para queries de lectura.
- Resiliencia con Polly (retry/circuit breaker) — llega con la fase de integración.