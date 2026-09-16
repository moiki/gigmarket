# 0007 - Idempotencia en `POST /api/orders`

## Problema
Un fallo de red o un `503` (catalog caído) deja al cliente sin saber si la orden se creó. Su retry natural **duplica la orden**: cada llamada ejecuta un handler que persiste una orden nueva. Este es el riesgo alto que la spec 0006 agendó para 0007. Necesitamos que repetir la misma operación con la misma clave tenga exactamente el mismo efecto que la primera vez.

## Contrato de API

### Header `Idempotency-Key` (opcional)
- Nombre: `Idempotency-Key`.
- Valor: string opaco, no vacío tras `trim`, máx. **128** caracteres (convención Stripe: sin espacios alrededor; se recomienda UUID v4 al frontend, pero no se valida formato completo).
- **Opcional por diseño**: si el cliente no lo envía, el comportamiento es el de 0006 (cada llamada crea una orden). Para el frontend se recomienda enviarlo siempre en el POST de creación.
- Alcance de unicidad: **(buyerId, Idempotency-Key)** — la misma clave de distintos buyers no colisiona.

### Comportamiento con clave nueva (idempotency miss)
- Fila de idempotencia inexistente → flujo normal de 0006:
  - **201 Created** con `OrderResponse` + `Location: /api/orders/{id}`.
  - Solo tras éxito se persiste la fila de idempotencia (ver decisión de atomicidad).

### Comportamiento con clave repetida (idempotency hit)
Misma clave, mismo buyerId, **mismo payload** (mismo `gigId` y `buyerId`):
- **200 OK** con el `OrderResponse` de la orden original (`price`, `providerId`, ... reconstruidos desde la BD) + `Location`. **No** se crea otra orden.
- Header de señalización: `Idempotency-Replayed: true`.
- No se re-consulta el catálogo ni se re-valida el estado del gig.

### Errores (Problem Details, RFC 9457) — sobre el contrato de 0006
| Situación | Status | Code | Nota |
|---|---|---|---|
| `Idempotency-Key` vacío/whitespace o > 128 chars | `400` | `Order.InvalidIdempotencyKey` | Validación HTTP, vive en el endpoint |
| Misma clave, mismo buyer, **payload distinto** | `409` | `Order.IdempotencyKeyMismatch` | Cambiar gigId con una clave usada = bug del cliente |
| Gigs 404 / inactive / catalog 503 (con clave nueva) | `404` / `409` / `503` | — | Idéntico a 0006; el error **no** se persiste (no hay efecto que repetir) |
| Cada request con clave nueva en el mismo instante (carrera concurrente) | uno `201`, el otro `200 replay` | — | Lo resuelve el índice único + reintento de lectura (ver decisiones) |

## Reglas de negocio / invariantes
1. La clave se consulta **antes** de procesar: miss → procesar; hit → replay.
2. La colonna de idempotencia se persiste **solo en éxito** (201). Un error `404/409/503` no deja rastro de idempotencia; el cliente puede reintentar con la misma clave y obtendrá una ejecución fresca.
3. El hash del payload (`SHA256` de `gigId + buyerId`) se compara en el hit para detectar reuso de clave con payload distinto → `409`.
4. El replay devuelve siempre el estado **original** de la orden (snapshot), nunca re-evalúa el gig.
5. TTL de la fila: **24 h**. Pasado el TTL, la clave vuelve a funcionar como nueva. Limpieza: oportunista (se borra al leer una clave expirada); job de barrido en Sprint 4/Outbox.

## Casos borde
- Clave repetida con mismo payload → 200 replay, una sola orden en BD.
- Clave repetida con payload distinto (mismo buyer) → 409, sin orden nueva.
- Clave repetida con buyer distinto → órdenes independientes (201 cada una); la clave se scopea por buyer, no colisiona entre buyers.
- Dos requests concurrentes con la misma clave → un 201 y un 200 replay; una sola orden.
- Clave usada en un POST que falló (404/409/503) → no queda registrada; reintentar con la misma clave → 201.
- Clave expirada (>24h) → se borra y trata como nueva → 201.
- Sin header → dos POST idénticos crean dos órdenes (comportamiento 0006, documentado).

## Criterios de aceptación (testeable)
- [ ] Dado un `Idempotency-Key` nueva y un gig activo, cuando se hace `POST /api/orders`, entonces responde `201` y la columna de idempotencia queda registrada.
- [ ] Dado el mismo request repetido (misma clave, mismo payload), cuando se hace `POST /api/orders`, entonces responde `200` con `Idempotency-Replayed: true`, devuelve la **misma** `orderId` que el primer POST y en BD hay **una sola** orden.
- [ ] Dado la misma clave pero `gigId` distinto (mismo buyer), cuando se hace `POST /api/orders`, entonces responde `409` y no hay orden nueva.
- [ ] Dado la misma clave pero `buyerId` distinto, cuando se hace `POST /api/orders`, entonces responde `201` y se crean órdenes independientes (la clave se scopea por buyer).
- [ ] Dado un POST que falla (gig 404) con una clave X, cuando se reintenta el mismo request con esa clave, entonces responde `201` (la clave no quedó "quemada").
- [ ] Dado dos POSTs concurrentes con la misma clave y mismo payload, entonces se crea **una sola** orden (un 201 + un 200 replay).
- [ ] Las filas de idempotencia expiradas (> 24 h) se ignoran y la clave vuelve a funcionar.

## Decisión de arquitectura
- **Idempotencia = preocupación HTTP, no de dominio.** El `CreateOrderCommand`/`Order` **no cambian**; el `Idempotency-Key` es un header, no un parámetro de negocio. La orquestación vive en el endpoint (`Orders.Api`) y el almacenamiento en `Orders.Infrastructure`.
- **Atomicidad de orden + registro:** ambos usan el mismo `OrderDbContext` scoped. El endpoint orquesta una transacción explícita a través del puerto `ITransaction` (impl `EfTransaction` en Infrastructure, `BeginTransactionAsync` de EF): `mediator.Send` (el repo hace su `SaveChanges` dentro de la tx) + `IdempotencyStore.Record` (segundo `SaveChanges`), y solo `CommitAsync` si todo ok. Si el registro falla → rollback de la orden. **No** se introduce UnitOfWork (los repos siguen commiteando; la convención de 0006 intacta). El puerto permite que los tests de endpoint sustituyan una transacción no-op sin tocar Postgres.
- **Índice único `(BuyerId, Key)` como guardián de la carrera:** dos requests simultáneos con la misma clave: uno commitea, el otro obtiene `DbUpdateException` por violación del índice → rollback + relectura → replay `200`. Por eso la verificación pre-hander es solo una optimización; la garantía real es el constraint.
- **`IIdempotencyStore` (Application) + `EfIdempotencyStore` (Infrastructure):** interfaz de puerto, impl con EF sobre la tabla `idempotency_records`. Reusa Dependency Inversion, como `IGigCatalog`.
- **Hash del payload:** `SHA256(gigId + buyerId)` en hex; compara en el hit → detección de reuso con payload distinto.
- **Replay reconstruye desde BD** (`IOrderRepository.GetById` del `OrderId` almacenado) → sin serializar bodies ni cachear respuestas HTTP completas.
- **DDL:** migración EF Core nueva (`AddIdempotency`): tabla `idempotency_records` (`id` bigint identity, `buyer_id`, `key`, `request_hash`, `order_id`, `created_at`, `expires_at`; índice único `(buyer_id, key)`).
- **Sin middleware de buffering de respuesta:** descartado el patrón de caché HTTP global de idempotencia (caso típico de ASP.NET) porque añade complejidad de buffer/pipeline y no aporta sobre el modelo "hash + row explícita + replay". Válido para un API de escritura de un solo agregado.

## Fuera de alcance
- `GET /api/orders` (listar) y `GET /api/orders/{id}` — siguiente sprint
- Transiciones `Confirmed` / `Cancelled` — siguiente sprint
- Idempotencia obligatoria (header required) — se mantiene opcional; forzar será decisión cuando exista frontend real
- Claves globales (sin scope por buyer) o multi-replica / locks distribuidos (se asume réplica única en MVP)
- Job de limpieza de TTL en background (queda en Sprint 4 con el Outbox worker)
- Cache distribuida / almacenamiento fuera de Postgres
- Pagos, Outbox, Service Bus, autenticación (ver 0006)