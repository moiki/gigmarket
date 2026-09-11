# Referencia: Comunicación Distribuida y Microservicios

Úsala cuando el usuario trabaje en la comunicación entre `catalog-api`, `orders-api` y `notifications-worker`, o pregunte sobre resiliencia, eventos o Azure Functions.

## Síncrono vs Asíncrono — cómo decidir con el usuario

Pregúntale primero: "¿el que llama necesita la respuesta inmediata para continuar, o solo necesita saber que 'algo pasará'?"

- **Síncrono (REST/gRPC)**: el frontend pidiendo el catálogo, o `orders-api` validando en tiempo real que un gig existe antes de crear la orden.
- **Asíncrono (eventos vía Service Bus)**: `orders-api` avisando que "una orden fue creada" para que `notifications-worker` envíe un email — el creador de la orden no necesita esperar a que el email se envíe.

Regla práctica: si puedes tolerar que la acción secundaria pase "en algún momento después", es asíncrono. Si el usuario necesita el resultado ya, es síncrono.

## Outbox Pattern — el problema que resuelve

Explica primero el problema sin nombrarlo: "Si guardas la orden en SQL y *luego* publicas el evento a Service Bus como dos pasos separados, ¿qué pasa si el proceso muere justo entre el paso 1 y el paso 2?" → el evento se pierde, pero la orden sí existe. Inconsistencia.

Solución: guardar el evento a publicar en una tabla `OutboxMessages` **dentro de la misma transacción** que guarda la orden. Un proceso aparte (background worker o Azure Function con timer) lee la tabla y publica a Service Bus, marcando como enviado.

```
BEGIN TRANSACTION
  INSERT INTO Orders (...)
  INSERT INTO OutboxMessages (EventType, Payload, ...)
COMMIT

-- Proceso aparte, cada N segundos o disparado por CDC/trigger:
SELECT * FROM OutboxMessages WHERE Sent = 0
  -> publicar a Service Bus
  -> marcar Sent = 1
```

## Saga Pattern (orquestada vs coreografiada)

Solo introdúcelo si el flujo de negocio crece a más de 2 pasos con posibilidad de rollback (ej. "reservar gig → cobrar pago → confirmar orden", y si el pago falla hay que liberar la reserva).

- **Coreografiada**: cada servicio escucha eventos y reacciona, sin un director central. Más desacoplado, más difícil de trazar.
- **Orquestada**: un "orquestador" (puede ser una Azure Function o un servicio dedicado) le dice a cada paso qué hacer y maneja las compensaciones. Más fácil de entender y debuggear, pero es un punto central.

Para el MVP de GigMarket, con solo 2-3 pasos, probablemente basta con coreografía simple (eventos + Outbox). No sobre-ingenierices una Saga completa a menos que el usuario quiera explícitamente practicarla.

## Resiliencia con Polly

Cuando `orders-api` llama a `catalog-api` (o a cualquier dependencia externa) de forma síncrona, introduce:

- **Retry con backoff exponencial**: reintentar 3 veces ante fallos transitorios (timeouts, 503)
- **Circuit Breaker**: si el servicio de abajo falla repetidamente, deja de intentar por un tiempo (evita "tumbar" un servicio ya caído con más tráfico)
- **Timeout explícito**: nunca dejar una llamada HTTP sin timeout definido

```csharp
services.AddHttpClient<ICatalogClient, CatalogClient>()
    .AddPolicyHandler(Policy.WrapAsync(retryPolicy, circuitBreakerPolicy));
```

## Idempotencia en consumidores de mensajes

Pregunta socrática: "Service Bus garantiza *at-least-once delivery*. ¿Qué pasa si tu worker recibe el mismo mensaje de 'orden creada' dos veces?" → el usuario debe llegar a: hay que trackear `MessageId`/`EventId` procesados (tabla o cache) y descartar duplicados antes de re-ejecutar efectos secundarios (como reenviar un email).

## Azure Functions — cuándo tiene sentido

- Trigger de Service Bus para `notifications-worker`: encaja perfecto (reactivo, sin carga sostenida, escala a cero).
- No forzar Azure Functions en `catalog-api`/`orders-api`: son APIs con tráfico sostenido, mejor como Container Apps siempre-encendidos (evita cold starts en el camino crítico del usuario).

## Contratos de eventos — tratarlos como contratos de API

Los eventos publicados a Service Bus también son un contrato entre servicios. Deben:
- Vivir en `libs/BuildingBlocks.Eventing` compartido (o versionarse explícitamente si servicios evolucionan a ritmos distintos)
- Tener versión en el nombre o el payload (`OrderCreatedV1`)
- Documentarse en la spec de la feature (sección "Contrato de API / Eventos")
