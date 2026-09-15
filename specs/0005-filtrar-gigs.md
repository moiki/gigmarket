# 0005 - Buscar y filtrar gigs por categoría y precio

## Problema
El buyer puede explorar el catálogo paginado (spec 0002) pero no puede reducirlo a lo que le sirve. Necesita filtrar por **categoría** y **rango de precio** — el caso de uso "buscar y filtrar gigs por categoría/precio" del MVP (`proyecto-mvp-monorepo.md` #2). La infraestructura de filtrado ya existe (`GetGigs` filter/order/paginate en SQL desde spec 0004), solo falta exponer los nuevos filtros.

## Contrato de API
- **Endpoint:** `GET /api/gigs`
- **Query params nuevos** (además de `page`, `pageSize`, `status` existentes):
  - `category` (opcional) — filtra por categoría; valores válidos del enum `GigCategory` (`Music`, `Design`, `Programming`, `Tutoring`, `Other`), case-insensitive.
  - `minPrice` (opcional, `>= 0`) — precio mínimo **inclusivo**.
  - `maxPrice` (opcional, `> 0`) — precio máximo **inclusivo**.
- **Combinación:** los filtros se aplican con AND (un gig debe cumplir todos los presentes).
- **Response:** misma shape de spec 0002 (`items`, `page`, `pageSize`, `totalCount`, `totalPages`). `totalCount` refleja el total con los filtros aplicados.
- **Errores:** `400` con Problem Details si `category` no es válida, `minPrice`/`maxPrice` no son decimales, negativos, o `minPrice > maxPrice`.

## Reglas de negocio / invariantes
1. Los filtros solo aplican al listado público → si no se envía `status`, se mantiene el default `Active`.
2. `minPrice` y `maxPrice` son inclusive: un gig de precio exactamente `minPrice` o `maxPrice` SÍ aparece.
3. Si `minPrice > maxPrice`, la combinación es imposible → `400` (contrato explícito) en vez de resultado vacío.
4. `category` se parsea case-insensitive igual que `status` (spec 0002): `Enum.TryParse(... ignoreCase: true)` + `Enum.IsDefined`.
5. El filtrado se ejecuta en la base de datos (SQL parametrizado en `GetGigs`), nunca en memoria en el endpoint.
6. La ausencia de un filtro no altera los otros: `GET /api/gigs?category=Music` sin precios devuelve todas las Music activas.

## Casos borde
- `category=music` (minúsculas) → 200, mismo resultado que `category=Music`.
- `category=Sports` (inexistente) → 400.
- `minPrice=-5` o `maxPrice=-1` → 400.
- `minPrice=30&maxPrice=10` → 400.
- `minPrice=abc` (no decimal) → 400.
- `minPrice=25&maxPrice=25` → 200 con gigs de precio exactamente `25` (ambos inclusivos).
- Filtros combinados sin resultados → 200 con `items: []`, `totalCount: 0`.
- `minPrice` o `maxPrice` con precisión decimal (ej. `25.5`) → 200, comparación decimal correcta.
- Gigs `Draft` con categoría/precio coincidentes → nunca aparecen sin `status=Draft`.

## Criterios de aceptación (testeable)
- [ ] Dado un catálogo con gigs de varias categorías, cuando se hace `GET /api/gigs?category=Music`, entonces responde `200` con solo gigs `Active` de `Music` y `totalCount` correcto.
- [ ] Dado un catálogo con precios variados, cuando se hace `GET /api/gigs?minPrice=30&maxPrice=50`, entonces responde `200` con solo gigs entre 30 y 50 inclusive.
- [ ] Dado un gig de precio `25`, cuando se hace `GET /api/gigs?minPrice=25&maxPrice=25`, entonces `25` SÍ aparece (límites inclusivos).
- [ ] Dado un catálogo de música, cuando se hace `GET /api/gigs?category=music`, entonces responde `200` con los mismos resultados que `category=Music` (case-insensitive).
- [ ] Dado `category=Sports` o `minPrice>maxPrice` o precio negativo, cuando se hace el request, entonces responde `400`.
- [ ] Dado `minPrice=30&maxPrice=50&category=Music`, cuando los hay, entonces responde solo gigs que cumplen TODOS los filtros (AND).
- [ ] Dado un gig `Draft` que cumple los filtros, cuando se hace `GET /api/gigs?category=Music` sin `status`, entonces NO aparece (default Active).

## Decisión de arquitectura
**Filtrado en la DB, no en memoria.** Los filtros se añaden como parámetros opcionales a `GetGigs(GigStatus, Page, PageSize)` (paso a `GetGigs(GigStatus, Category?, decimal?, decimal?, Page, PageSize)` o un objeto filtro) y el `EfGigRepository` construye el `Where` compuesto en SQL parametrizado — EF Core lo traduce con parámetros (no concatenación, sin riesgo de inyección). El `InMemoryGigRepository` y el `GetGigsQueryHandler` quedan como pasamanos con la misma semántica.

Esto consolida la decisión de la spec 0004: el repo es la única frontera con datos y toda lectura pesada vive en la base de datos. **Descartado:** filtrar en memoria en el handler o en la capa API — no escala y divide la lógica de lectura en dos lugares.

**Contrato tipo vs. primitivos:** se usan primitivos (`Category?`, `decimal?`) en la query CQRS en vez de un objeto filtro, porque hoy son solo 3 y el shape del query param del endpoint se mapea directo. Si crecen (texto, sort, radios) se migra a un `GigSearchCriteria` — la firma de `IGigRepository` lo permite sin tocar Application.

## Fuera de alcance
- Búsqueda por texto/libre (keyword en `title`/`description`) — misma mecánica de query param, spec aparte cuando aplique.
- Ordenamiento configurable por precio o relevancia (solo `createdAt` desc por ahora).
- Filtros de owner/admin (ver gigs propios, por estado) — el `ownerId` aún no está autenticado.
- Endpoint de detalle `GET /api/gigs/{id}` (spec aparte).
- Range filtering para `createdAt`.

## Tareas de implementación (post-aprobación)
1. `GetGigsQuery`: añadir `Category?`, `MinPrice?`, `MaxPrice?`.
2. `IGigRepository.GetGigs`: extender firma (o recibir un filtro) + implementar en `EfGigRepository` (Where compuesto, inclusive) y `InMemoryGigRepository`.
3. Endpoint: parsear `category`/`minPrice`/`maxPrice` con validaciones → `400` en inválidos (mismo patrón que `TryParseStatus`).
4. Tests endpoint (aprox. 7 criterios) + extending integration test de paginación con filtro.