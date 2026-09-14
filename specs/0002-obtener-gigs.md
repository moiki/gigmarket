# 0002 - Obtener Gigs (listado paginado)

## Problema
Un buyer debe poder explorar el catálogo de servicios disponibles con paginación, filtrando solo los gigs publicados (`Active`), ordenados por los más nuevos primero. Es el primer caso de lectura del catálogo y el criterio de aceptación 6 pendiente de la spec 0001.

## Contrato de API
- **Endpoint:** `GET /api/gigs`
- **Query params:**
  - `page` (opcional, default `1`, mínimo `1`) — número de página, paginación por offset.
  - `pageSize` (opcional, default `20`, rango `1..100`) — items por página.
  - `status` (opcional, default `Active`) — filtra por estado; valores válidos del enum `GigStatus` (`Draft`, `Active`).
- **Ordenamiento:** `createdAt` descendente (más nuevos primero). Sin param de sort por ahora.
- **Response 200 OK:**
  ```json
  {
    "items": [
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
    ],
    "page": 1,
    "pageSize": 20,
    "totalCount": 142,
    "totalPages": 8
  }
  ```
- **Errores:** `400` con Problem Details si `page`, `pageSize` o `status` son inválidos.

## Reglas de negocio / invariantes
1. El caso de uso principal es búsqueda pública → si no se envía `status`, se devuelven solo gigs `Active`.
2. Nunca devolver una página vacía como error: si `page` supera el total, `200` con `items: []`.
3. `totalCount` es el total de gigs que cumplen el filtro (no el total de la página).
4. El shape de cada item debe coincidir con el del `POST /api/gigs` (spec 0001).

## Casos borde
- `page = 0` o negativo → 400
- `pageSize = 0` o `> 100` → 400
- `status` no existe en el enum (ej. `"Deleted"`) → 400
- Catálogo vacío → 200 con `items: []`, `totalCount: 0`, `totalPages: 0`
- No hay gigs `Active` pero sí `Draft` → 200 con `items: []` (el default filtra `Active`)
- `page` más allá del total → 200 con `items: []`, sin error

## Criterios de aceptación (testeable)
- [ ] Dado un catálogo con N gigs `Active`, cuando se hace `GET /api/gigs` sin params, entonces responde `200` con items ordenados por `createdAt` descendente, `pageSize` por defecto 20 y metadata de paginación consistente (`totalCount` = N).
- [ ] Dado un catálogo mezclado (gigs `Active` y `Draft`), cuando se hace `GET /api/gigs` sin `status`, entonces solo se devuelven gigs `Active`.
- [ ] Dado un catálogo con gigs `Draft`, cuando se hace `GET /api/gigs?status=Draft`, entonces solo se devuelven gigs `Draft`.
- [ ] Dado un `page` más allá del total, cuando se hace `GET /api/gigs?page=999`, entonces responde `200` con `items: []` y `totalCount` correcto.
- [ ] Dado `page=0`, `pageSize=0` o `pageSize=500`, cuando se hace `GET /api/gigs` con esos params, entonces responde `400`.
- [ ] Dado `status=Deleted`, cuando se hace `GET /api/gigs?status=Deleted`, entonces responde `400`.
- [ ] Dado un catálogo vacío, cuando se hace `GET /api/gigs`, entonces responde `200` con `items: []`, `totalCount: 0`, `totalPages: 0`.

## Decisión de arquitectura
**Decisión (con el usuario): Opción B — CQRS + MediatR.**

Se introduce el patrón CQRS separando la escritura (Comando) de la lectura (Query), con MediatR como mediador. Esto implica:

- Migrar `POST /api/gigs` de `CreateGigService` a un `CreateGigCommand` con su `IRequestHandler`.
- Crear `GetGigsQuery` con su `IRequestHandler` para la lectura paginada.
- Ampliar `IGigRepository` con una consulta paginada `GetGigs(...)` que devuelva items + total (el handler orquesta el filtrado/orden/paginación; el repo puede hacerlo o devolver la colección completa — se decide en implementation, con el default `Active` aplicado en Application para que el repositorio siga siendo tonto).
- El refactor del POST se protege con los 18 tests existentes como red de seguridad.

**Por qué CQRS y por qué ahora:** las lecturas y las escrituras tienen requisitos opuestos (una query de catálogo pide optimización de lectura; un comando pide integridad de escritura). Mezclarlas hace que cada modelo sea un promedio de ambos. La spec 0001 lo difirió a este Sprint a propósito. Es el entregable de la Fase 1 del roadmap y un requisito explícito de la oferta ("CQRS, Mediator patterns, MediatR").

**Por qué MediatR y no un bus propio:** es el estándar de facto en .NET para CQRS/mediación en proceso (sin red, sin infraestructura extra). Para un API single process es la elección correcta; un bus de integración (Service Bus) llegará en la Fase 2 para lo cross-service.

**Descartado:** seguir con services manuales (Opción A) porque no nos acerca al entregable de la Fase 1; y CQRS con dos modelos de datos separados (uno de lectura optimizado, uno de escritura) porque es premature — la misma proyección sirve para ambos hoy.

## Fuera de alcance
- Filtro por categoría y búsqueda por texto (se añaden luego al mismo query param pattern).
- Ordenamiento configurable (solo `createdAt` desc por ahora).
- Persistencia real (Cosmos DB) — se mantiene `InMemoryGigRepository` ampliado.
- Autenticación / ver que el `ownerId` exista.
- Cursor-based pagination.
- Endpoint de detalle `GET /api/gigs/{id}` (spec aparte).