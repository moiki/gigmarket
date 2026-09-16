# 0008 - Consultar órdenes y transiciones de estado

## Problema
Hoy `orders-api` solo sabe crear una orden (`POST /api/orders`, spec 0006/0007). No hay forma de **leer** lo que existe (ni una orden por id, ni un listado con filtros) ni de **mover** una orden de su estado inicial `Created`. Sin esto el agregado es un callejón sin salida: el comprador no puede ver su orden, el proveedor no puede confirmarla y nadie puede cancelarla. Esta spec cierra el ciclo básico del agregado `Order`.

## Contrato de API

### `GET /api/orders/{id}`
- **200 OK** con `OrderResponse` si la orden existe.
- **404** `Order.NotFound` si no existe.

### `GET /api/orders`
Listado paginado con filtros opcionales. Query params:
| Param | Tipo | Default | Regla |
|---|---|---|---|
| `page` | int | `1` | `>= 1`, si no → `400` |
| `pageSize` | int | `20` | entre `1` y `100`, si no → `400` |
| `status` | enum `OrderStatus` | *sin filtro* | si viene y no es `Created`/`Confirmed`/`Cancelled` → `400` |
| `buyerId` | guid | *sin filtro* | opcional |
| `providerId` | guid | *sin filtro* | opcional |

- **200 OK** con `GetOrdersResponse` (`items`, `page`, `pageSize`, `totalCount`, `totalPages`).
- Orden por `createdAt` **descendente** (más recientes primero), igual que los gigs.
- Sin `status` → devuelve **todos** los estados (a diferencia de gigs, que por defecto filtra `Active`): una orden nace `Created` y la lista debe mostrarla.

### `POST /api/orders/{id}/confirm`
- **200 OK** con el `OrderResponse` ya en `Confirmed`.
- **404** `Order.NotFound` si no existe.
- **409** `Order.InvalidStatusTransition` si la orden no está en `Created`.

### `POST /api/orders/{id}/cancel`
- **200 OK** con el `OrderResponse` ya en `Cancelled`.
- **404** `Order.NotFound` si no existe.
- **409** `Order.InvalidStatusTransition` si la orden ya está en `Cancelled`.

## Máquina de estados
```
Created --confirm--> Confirmed --cancel--> Cancelled
   \                                          ^
    \---------------cancel--------------------/
```
- `Created → Confirmed` (proveedor confirma).
- `Created → Cancelled` (comprador cancela antes de confirmar).
- `Confirmed → Cancelled` (cancelación post-confirmación).
- `Cancelled` es **terminal**: `confirm` o `cancel` sobre `Cancelled` → `409`.
- `confirm` sobre `Confirmed` → `409` (no hay transición a sí misma).

## Casos borde
- `GET /api/orders/{id}` de un guid inexistente → 404 (no 400).
- Listado sin resultados → `200` con `items: []`, `totalCount: 0`, `totalPages: 0`.
- `page` más allá del total → `200` con `items: []` (mismo comportamiento que gigs).
- `status=Confirmed` filtra; un valor inválido (`status=Nope`) → 400 `InvalidQueryParameters`.
- `confirm` sobre `Created` → 200; segundo `confirm` sobre la misma orden → 409.
- `cancel` sobre `Created` o `Confirmed` → 200; `cancel` sobre `Cancelled` → 409.

## Criterios de aceptación (testeable)
- [ ] Dado una orden existente, `GET /api/orders/{id}` responde `200` con sus datos.
- [ ] Dado un id inexistente, `GET /api/orders/{id}` responde `404 Order.NotFound`.
- [ ] Dado varias órdenes, `GET /api/orders` pagina y ordena por fecha descendente.
- [ ] Dado `status=Confirmed`, `GET /api/orders` solo devuelve órdenes confirmadas.
- [ ] Dado `status=Nope` o `pageSize=0`, `GET /api/orders` responde `400`.
- [ ] Dado `Created`, `POST /api/orders/{id}/confirm` responde `200` con `status=Confirmed`.
- [ ] Confirmar una orden ya `Confirmed` responde `409 Order.InvalidStatusTransition`.
- [ ] Cancelar una orden `Created` responde `200` con `status=Cancelled`.
- [ ] Cancelar una orden `Confirmed` responde `200` con `status=Cancelled`.
- [ ] Cancelar/confirmar una orden `Cancelled` responde `409`.
- [ ] El cambio de estado **persiste** (una lectura posterior lo devuelve).
- [ ] `POST /api/orders/{id}/confirm` de un id inexistente responde `404`.

## Decisión de arquitectura
- **Las transiciones son métodos de dominio, no del handler.** `Order.Confirm()` / `Order.Cancel()` devuelven `Result<Order>` y validan la máquina de estados con `OrderErrors.InvalidStatusTransition`. El handler solo orquesta (`GetById` → método → `Update`), igual que `PublishGigCommandHandler`.
- **`Result<Order>` para transiciones (comando), dato directo para lecturas.** Coherente con la convención: un comando puede fallar por estado inválido; una query con parámetros válidos no falla. `GetOrderByIdQuery → Order?` (como `GetGigByIdQuery`) y `GetOrdersQuery → PagedResult<Order>`.
- **`POST .../confirm` y `POST .../cancel` (no `PATCH`).** Mismo estilo que `POST /api/gigs/{id}/publish`; transiciones explícitas, sin body ni máquina de campos parciales.
- **Sin `Idempotency-Key` en las transiciones.** La repetición ya es segura a nivel de estado: repetir `confirm` es un no-op lógico que responde `409`, no un duplicado. No hay efecto nuevo que proteger.
- **`PagedResult<T>` se mueve a `BuildingBlocks.Common`.** Ambas APIs paginan; el sobre (`items`, `page`, `pageSize`, `totalCount`, `totalPages`) es un primitivo compartido. `orders-api` no puede referenciar `Catalog.Application`, así que duplicarlo o subirlo son las opciones; se sube al shared kernel y `catalog-api` solo ajusta el `using`.
- **`IOrderRepository` crece con `Update(Order)` y `GetOrders(filtros, page, pageSize)`.** Espeja `IGigRepository`. `Update` hace `Update` + `SaveChanges` (la convención de "el repo commitea" de 0006 se mantiene; sin UnitOfWork).
- **Sin migración:** los endpoints no cambian el esquema (`orders.status` ya existe como `varchar(10)`), así que no hay `dotnet ef migrations add`.
- **Sin filtro obligatorio por dueño:** no hay autenticación todavía; cualquiera puede listar/confirmar/cancelar. El ownership (¿solo el proveedor confirma? ¿solo el comprador cancela?) queda para el sprint de auth.

## Fuera de alcance
- Autenticación/autorización y reglas de ownership (comprador vs proveedor) — sprint de auth
- Eventos de dominio / Outbox y notificaciones por cambio de estado — Sprint 4 (outbox + worker)
- `Confirmed → Completed` (entrega), reembolsos, pagos
- Borrado de órdenes / soft delete
- Idempotencia en las transiciones (ver decisión)
- Ordenamientos o búsquedas configurables más allá de `createdAt DESC`
