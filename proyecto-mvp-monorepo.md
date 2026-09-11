# Proyecto MVP: "GigMarket" — Monorepo React + .NET en Azure

Un marketplace B2C simplificado (piensa "Fiverr/Airbnb de servicios locales") diseñado a propósito para tocar **todos** los requisitos técnicos de la oferta de Franki: APIs REST, CQRS/MediatR, SQL Server + Cosmos DB, eventos asíncronos, Azure Functions, CI/CD y despliegue completo en Azure.

No es una app de todo-lista disfrazada: incluye catálogo, búsqueda, reservas/órdenes y notificaciones — suficiente complejidad real para justificar microservicios sin sobre-ingeniería.

---

## 1. Concepto funcional del MVP

Un usuario puede:
1. Publicar un **servicio/gig** (ej. "Clases de guitarra", "Diseño de logo") → catálogo
2. Buscar y filtrar gigs por categoría/precio
3. **Reservar/contratar** un gig → genera una orden
4. Recibir **notificaciones** (email/push simulado) cuando cambia el estado de su orden

Con esto ya tienes: catálogo (read-heavy), transacciones (write-heavy, consistencia), y eventos (notificaciones asíncronas) — los 3 patrones de acceso a datos más comunes en el mundo real.

---

## 2. Estructura del monorepo

```
gigmarket/
├── apps/
│   └── web/                      # React + TS + Vite (frontend)
│       ├── src/
│       └── package.json
│
├── services/
│   ├── catalog-api/               # .NET 10 - Minimal API + Clean Architecture
│   │   ├── Catalog.Api/
│   │   ├── Catalog.Application/   # CQRS + MediatR
│   │   ├── Catalog.Domain/
│   │   └── Catalog.Infrastructure/# Cosmos DB
│   │
│   ├── orders-api/                # .NET 10 - Clean Architecture
│   │   ├── Orders.Api/
│   │   ├── Orders.Application/    # CQRS + MediatR + Outbox
│   │   ├── Orders.Domain/
│   │   └── Orders.Infrastructure/ # Azure SQL (EF Core)
│   │
│   └── notifications-worker/      # Azure Function (Service Bus trigger)
│
├── libs/
│   ├── BuildingBlocks.Common/     # Result<T>, excepciones base, paginación
│   ├── BuildingBlocks.Eventing/   # Contratos de eventos + abstracción Service Bus
│   └── BuildingBlocks.Observability/ # Serilog + OpenTelemetry setup compartido
│
├── infra/
│   └── bicep/
│       ├── main.bicep
│       ├── modules/
│       │   ├── sql.bicep
│       │   ├── cosmos.bicep
│       │   ├── service-bus.bicep
│       │   ├── container-apps.bicep
│       │   ├── static-web-app.bicep
│       │   └── apim.bicep
│       └── environments/
│           ├── dev.bicepparam
│           └── prod.bicepparam
│
├── specs/                         # Specs SDD por feature (ver skill open-net-teacher)
│   └── 0001-crear-gig.md
│
├── .github/workflows/             # o /pipelines si usas Azure DevOps
│   ├── ci.yml
│   ├── deploy-catalog-api.yml
│   ├── deploy-orders-api.yml
│   ├── deploy-notifications.yml
│   └── deploy-web.yml
│
├── docker-compose.yml             # SQL Server + Cosmos Emulator + Service Bus emulator local
└── README.md
```

**Por qué monorepo:** un solo lugar para ver frontend + backend + infra + specs, PRs atómicos que tocan API y UI a la vez, y un pipeline de CI que puede correr solo lo que cambió (path filters).

---

## 3. Arquitectura técnica

```
┌─────────────┐        ┌──────────────────────┐
│  React App  │──────▶│  Azure API Management │  (Gateway, auth, rate limit)
│ (Static Web │        └──────────┬────────────┘
│    App)     │                   │
└─────────────┘        ┌──────────┴───────────┐
                        ▼                      ▼
              ┌──────────────────┐   ┌──────────────────┐
              │   Catalog API     │   │    Orders API     │
              │  (Container App)  │   │  (Container App)  │
              │  CQRS + MediatR   │   │  CQRS + MediatR   │
              └────────┬──────────┘   └────────┬──────────┘
                       │                        │
                       ▼                        ▼
                 ┌──────────┐            ┌─────────────┐
                 │ Cosmos DB │            │  Azure SQL   │
                 └──────────┘            └──────┬───────┘
                                                  │ Outbox
                                                  ▼
                                         ┌──────────────────┐
                                         │  Service Bus      │
                                         │ (topic: orders)   │
                                         └────────┬───────────┘
                                                  ▼
                                     ┌──────────────────────┐
                                     │ Notifications Worker  │
                                     │   (Azure Function)    │
                                     └──────────────────────┘

  Todo instrumentado con Application Insights / OpenTelemetry
  Secretos en Azure Key Vault, identidades vía Managed Identity
```

### Decisiones clave (para tus ADRs)
| Decisión | Por qué |
|---|---|
| Catalog → Cosmos DB | Lecturas de catálogo son documentos semi-estructurados con alta cardinalidad de filtros; escala horizontal barata |
| Orders → Azure SQL | Necesita transacciones ACID e integridad referencial (orden ↔ pago ↔ estado) |
| Comunicación orders→notifications vía eventos | Desacopla; si el worker cae, los mensajes esperan en la cola (resiliencia) |
| Outbox Pattern en Orders | Evita el problema de "guardé en SQL pero no publiqué el evento" (doble escritura) |
| API Management como gateway | Punto único de auth (Entra ID / B2C), rate limiting, y versionado de contratos |
| Container Apps para APIs, Functions para el worker | Las APIs tienen carga sostenida; el worker es puramente reactivo a eventos → serverless tiene sentido ahí |

---

## 4. Roadmap de implementación (mapea 1:1 con `roadmap-dotnet-senior.md`)

| Sprint | Entregable | Fase del roadmap |
|---|---|---|
| 1 | Monorepo scaffolding + `catalog-api` con 1 endpoint (Clean Architecture, sin CQRS aún) + tests unitarios | Fase 0 |
| 2 | `catalog-api` con CQRS/MediatR completo + Cosmos DB real | Fase 1 |
| 3 | `orders-api` con Clean Architecture + Azure SQL + EF Core | Fase 1-2 |
| 4 | Comunicación async: Outbox + Service Bus + `notifications-worker` (Azure Function) | Fase 2 |
| 5 | Specs SDD retroactivas + tests de integración con Testcontainers + resiliencia (Polly) | Fase 3 |
| 6 | CI/CD completo (build/test/deploy) + Bicep para todos los recursos | Fase 4 |
| 7 | Frontend React consumiendo APIs vía APIM + deploy en Static Web Apps | Fase 4 |
| 8 | Observabilidad end-to-end (traces distribuidos), hardening de seguridad, ADRs finales | Fase 4-5 |

---

## 5. Stack técnico resumen

**Backend:** .NET 10 (LTS), Minimal APIs, MediatR, FluentValidation, EF Core, Dapper (para queries de lectura pesadas si aplica), Polly, Serilog, xUnit, Testcontainers, ArchUnitNET

**Frontend:** React 18 + TypeScript + Vite, TanStack Query (fetching/cache), Zod (validación de contratos compartidos con OpenAPI), Tailwind

**Azure:** Container Apps, Azure Functions, Azure SQL, Cosmos DB, Service Bus, API Management, Static Web Apps, Key Vault, Application Insights, Azure DevOps o GitHub Actions

**Infra:** Bicep, Docker/Docker Compose para entorno local

---

## Siguiente paso

Usa la skill `open-net-teacher` para arrancar el Sprint 1: te va a pedir primero la spec de "publicar un gig" antes de dejarte escribir una sola línea de código.
