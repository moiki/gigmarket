# 0001 - Crear Gig

## Problema
Un provider debe poder publicar un servicio (gig) en el catálogo. Es el primer paso funcional del marketplace: sin gigs no hay catálogo que buscar ni órdenes que reservar.

## Contrato de API
- **Endpoint:** `POST /api/gigs`
- **Request:**
  ```json
  {
    "title": "Clases de guitarra",
    "description": "Nivel inicial",
    "price": 25,
    "category": "Music",
    "ownerId": "9f0b1a8c-1234-4a5b-8c9d-0e1f2a3b4c5d"
  }
  ```
- **Response 201 Created:**
  ```json
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "title": "Clases de guitarra",
    "description": "Nivel inicial",
    "price": 25,
    "category": "Music",
    "status": "Draft",
    "ownerId": "9f0b1a8c-1234-4a5b-8c9d-0e1f2a3b4c5d",
    "createdAt": "2026-09-11T16:00:00Z"
  }
  ```
  Con header `Location: /api/gigs/{id}`.
- **Errores:** `400` si el payload no es JSON válido; `422 Unprocessable Entity` con Problem Details (RFC 7807) si falla una regla de negocio.

## Reglas de negocio / invariantes (se validan al construir el `Gig`, nunca estado inválido en memoria)
1. `title` requerido, 1..100 caracteres (trimmed).
2. `description` opcional, máx. 2000 caracteres.
3. `price` decimal: `0 < price <= 100000`.
4. `category` es un enum cerrado: `Music | Design | Programming | Tutoring | Other`.
5. `ownerId` es un `Guid` no vacío (aún no existe servicio de usuarios).
6. El gig nace en estado `Draft`.
7. `id` y `createdAt` los genera el sistema.

## Casos borde
- `title` vacío o solo espacios → 422
- `title` > 100 caracteres → 422
- `price` = 0, negativo o > 100000 → 422
- `category` no existente en el enum → 422
- `ownerId` vacío / Guid zero → 422
- `description` mayor a 2000 → 422

## Criterios de aceptación (testeable)
- [ ] Dado un request válido, cuando se hace `POST /api/gigs`, entonces responde `201` con el gig creado en estado `Draft`, `Location` header, e `id`/`createdAt` generados.
- [ ] Dado un `title` vacío, cuando se hace `POST /api/gigs`, entonces responde `422` y el gig **no** se crea.
- [ ] Dado un `price` de 0 o negativo, cuando se hace `POST /api/gigs`, entonces responde `422` y el gig **no** se crea.
- [ ] Dado un `category` inválido ("Cooking"), cuando se hace `POST /api/gigs`, entonces responde `422` y el gig **no** se crea.
- [ ] Dado un `ownerId` Guid zero, cuando se hace `POST /api/gigs`, entonces responde `422` y el gig **no** se crea.
- [ ] Dado un gig creado con éxito, el repositorio puede devolverlo (prepara el `GET` del sprint 2).

## Decisión de arquitectura
Clean Architecture **sin CQRS aún** (llega en el Sprint 2 con MediatR). Capas:
- `Catalog.Domain` → entidad `Gig` + `GigCategory` + errores de dominio. Sin dependencias externas.
- `Catalog.Application` → puerto `IGigRepository` + servicio de creación que devuelve `Result<Gig>`. Depende solo de Domain.
- `Catalog.Infrastructure` → `InMemoryGigRepository` (implementa el puerto). Depende de Application + Domain.
- `Catalog.Api` → Minimal API que mapea el request, llama al servicio y traduce `Result<T>` a HTTP status. Depende de Application + Infrastructure.

**Por qué Repository ahora, si es en memoria:** Dependency Inversion — cuando llegue Cosmos DB (Sprint 2) solo cambia Infrastructure, sin tocar Application ni Domain.
**Por qué `Result<T>` y no excepciones para control de flujo:** la validación de negocio es un resultado esperado, no una falla del sistema.

**Descartado:** validación dentro del endpoint (fuga de reglas a la capa API), excepciones de dominio para validación, excederse en arquitectura (sin EventBus, sin UnitOfWork, sin paginación aquí).

## Fuera de alcance
- `GET /api/gigs` (listar/buscar/filtrar) — Sprint 2
- Publicación del gig (`Draft → Active`) — Sprint 2
- Persistencia real (Cosmos DB) — Sprint 2
- CQRS / MediatR — Sprint 2
- Autenticación / autorización / verificar que el owner exista
- Edición, borrado, reviews