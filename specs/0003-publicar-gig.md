# 0003 - Publicar gig (Draft → Active)

## Problema
Un provider debe poder publicar un gig para que sea visible en el catálogo público (los buyers solo ven `Active`, spec 0002). Es la transición de estado de dominio que la spec 0002 ya preparó internamente con `Gig.Publish()`.

## Contrato de API
- **Endpoint:** `POST /api/gigs/{id}/publish`
- **Path param:** `id` (Guid del gig)
- **Sin body.**
- **Response 200 OK:**
  ```json
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "title": "Clases de guitarra",
    "description": "Nivel inicial",
    "price": 25,
    "category": "Music",
    "status": "Active",
    "ownerId": "9f0b1a8c-1234-4a5b-8c9d-0e1f2a3b4c5d",
    "createdAt": "2026-09-11T16:00:00Z"
  }
  ```
- **Errores:**
  - `404` con Problem Details si el gig no existe.
  - `422` con Problem Details (`Gig.NotDraftStatus`) si el gig ya está `Active`.
  - `400` si `id` no es un Guid válido.

## Reglas de negocio / invariantes
1. Solo un gig en estado `Draft` puede publicarse (regla ya en `Gig.Publish()`).
2. Publicar es irreversible: `Draft → Active`, sin retorno por ahora.
3. El estado publicado es la fuente de verdad para `GET /api/gigs` (un gig `Active` aparece en el listado público).
4. Publicar dos veces no es idempotente: falla con regla de negocio.

## Casos borde
- `id` válido pero inexistente → 404
- `id` mal formado → 400
- gig ya `Active` → 422 con `Gig.NotDraftStatus`, sin cambio de estado
- gig `Draft` correcto → 200 con `status: Active`

## Criterios de aceptación (testeable)
- [ ] Dado un gig en `Draft`, cuando se hace `POST /api/gigs/{id}/publish`, entonces responde `200` con el gig en `status: Active`.
- [ ] Dado un gig en `Draft` recién publicado, cuando se hace `GET /api/gigs` (default `Active`), entonces el gig aparece en el listado.
- [ ] Dado un gig ya `Active`, cuando se hace `POST /api/gigs/{id}/publish`, entonces responde `422` con el error `Gig.NotDraftStatus` y el estado sigue `Active`.
- [ ] Dado un `id` que no existe, cuando se hace `POST /api/gigs/{id}/publish`, entonces responde `404`.
- [ ] Dado un `id` mal formado, cuando se hace `POST /api/gigs/{id}/publish`, entonces responde `400`.

## Decisión de arquitectura
- `PublishGigCommand(Guid GigId)` + `PublishGigCommandHandler` en `Catalog.Application`, siguiendo los estándares del skill `dotnet-cqrs` (command → `Result<T>`, datos y validación separados por capa).
- **Mapeo de error a status HTTP por código:** `GigErrors.GigNotFound` → 404, `GigErrors.NotDraftStatus` → 422. Primera vez en el proyecto donde un comando falla de dos formas distintas con status HTTP diferentes.
- `Gig.Publish()` pasa de `internal` a `public`: la transición preparada en la spec 0002 (como puente para tests del listado) ahora es parte del producto. Se conserva el `InternalsVisibleTo` para los tests existentes.
- Se agrega `GigErrors.GigNotFound` en el dominio (recurso inexistente = resultado esperado, no excepción).
- El repositorio no cambia: `GetById` ya existe y la mutación ocurre sobre el mismo objeto en memoria.

## Fuera de alcance
- `GET /api/gigs/{id}` (detalle de un gig) — spec aparte.
- Unpublish / `Active → Draft` — no es regla de negocio.
- Edición del gig.
- Autenticación / verificar que el `ownerId` del request sea dueño del gig (aún no hay servicio de usuarios).