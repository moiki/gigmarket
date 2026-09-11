# Roadmap: De Backend Dev a Senior .NET / Azure Engineer
### Basado en la oferta "Senior Backend Engineer - LATAM" (Franki)

Este roadmap traduce cada requisito de la oferta en una ruta de aprendizaje concreta, ordenada en 4 fases de ~3 meses cada una (ajustable a tu ritmo). Está pensado para ejecutarse **en paralelo** con el proyecto monorepo (`proyecto-mvp-monorepo.md`) y guiado por la skill `open-net-teacher`.

---

## Cómo leer este documento

Cada fase tiene:
- **Objetivo de negocio** (qué te habilita a hacer en un trabajo real)
- **Temas técnicos** a dominar
- **Requisito de la oferta que cubre** (para que veas el mapeo directo)
- **Entregable práctico** dentro del proyecto MVP
- **Criterio de "hecho"** (definition of done del aprendizaje, no solo "leí sobre esto")

---

## Fase 0 — Fundamentos sólidos de senioridad (2-3 semanas)

Antes de arquitectura, hay que blindar las bases que un senior no debería tropezar:

| Tema | Detalle |
|---|---|
| C# moderno (10/11/12) | records, pattern matching, nullable reference types, `required`, source generators básicos |
| SOLID aplicado (no de memoria) | ejemplos reales de violación → refactor |
| Clean Code | nombres, funciones pequeñas, manejo de errores con `Result<T>`/excepciones, evitar side-effects ocultos |
| Testing mindset | pirámide de tests, AAA pattern, test doubles (mock/stub/fake) |
| Git avanzado | trunk-based vs gitflow, rebase interactivo, conventional commits |

**Cubre de la oferta:** "Strong expertise in C#, .NET, .NET Core", cultura de code review.

**Entregable:** setup del monorepo + primer servicio "hello world" con tests desde el día 1.

---

## Fase 1 — Arquitectura y patrones de diseño (mes 1-3)

**Objetivo de negocio:** poder liderar decisiones de arquitectura, no solo ejecutar tickets.

### 1.1 Arquitecturas de aplicación
- Clean Architecture / Onion / Hexagonal (Ports & Adapters) — elegir una y justificar por qué
- Separación por capas: Domain, Application, Infrastructure, API
- Vertical Slice Architecture como alternativa (útil en microservicios pequeños)

### 1.2 Domain-Driven Design (nivel práctico, no académico)
- Entidades vs Value Objects vs Aggregates
- Domain Events
- Bounded Contexts (clave si luego hay microservicios)

### 1.3 Patrones de diseño con aplicación real en .NET
- Repository + Unit of Work (y cuándo **no** usarlos con EF Core)
- **CQRS** (bonus explícito en la oferta)
- **Mediator / MediatR** (bonus explícito en la oferta)
- Factory, Strategy, Decorator, Chain of Responsibility, Specification Pattern
- Result Pattern / Railway-oriented programming para manejo de errores sin excepciones de control de flujo

### 1.4 Diseño de APIs REST de nivel senior
- Versionado, HATEOAS (cuándo sí / cuándo no), paginación, filtering, idempotencia
- OpenAPI/Swagger como contrato, no como documentación tardía
- Validación (FluentValidation), Problem Details (RFC 7807)

**Cubre de la oferta:** "Architect and develop secure, reliable, and high-performance APIs", "software design patterns", "CQRS, Mediator patterns, MediatR".

**Entregable:** el servicio de catálogo del MVP implementado con Clean Architecture + CQRS/MediatR + Result Pattern.

**Definition of done:** puedes explicar en una entrevista *por qué* elegiste cada patrón y qué problema resuelve (no "porque es best practice").

---

## Fase 2 — Datos, comunicación distribuida y eventos (mes 3-6)

**Objetivo de negocio:** diseñar sistemas que escalan y se comunican de forma confiable.

### 2.1 Persistencia poliglota
- SQL Server / Azure SQL: modelado relacional, normalización, índices, query performance, EF Core avanzado (compiled queries, tracking vs no-tracking, migrations en CI/CD)
- **Cosmos DB**: modelado de documentos, partition keys, RU/consistency levels — pensar "NoSQL first" cuando aplica
- Cuándo usar cada una (y por qué el MVP usará ambas a propósito)

### 2.2 Comunicación entre servicios
- Síncrona: REST vs gRPC (trade-offs de latencia y contratos fuertes)
- Asíncrona / Event-Driven: **Azure Service Bus** (colas y tópicos), Event Grid
- Patrones de resiliencia: Retry, Circuit Breaker, Timeout (Polly)
- **Outbox Pattern** para consistencia entre DB y mensajería
- **Saga Pattern** (orquestada vs coreografiada) para transacciones distribuidas
- Idempotencia en consumidores de mensajes

### 2.3 Serverless
- **Azure Functions** (triggers HTTP, Service Bus, Timer)
- Cuándo un microservicio debería ser serverless vs contenedor siempre-encendido

**Cubre de la oferta:** "Cosmos DB, SQL Server", "distributed systems", "serverless applications and event-driven architectures", "Azure Functions".

**Entregable:** servicio de notificaciones como Azure Function disparada por eventos de Service Bus; comunicación orders-api ↔ catalog-api vía eventos.

**Definition of done:** puedes dibujar el diagrama de secuencia completo de una operación distribuida (ej. "crear orden") incluyendo qué pasa si un paso falla a mitad de camino.

---

## Fase 3 — Calidad, testing y Spec-Driven Development (mes 5-7, en paralelo con Fase 2)

**Objetivo de negocio:** que tu código sea confiable sin depender de QA manual, y que el diseño se decida *antes* de escribir código (no se descubra a mitad del PR).

### 3.1 SDD (Spec-Driven Development) aterrizado
- Escribir una **spec corta** (problema, contrato de API/eventos, criterios de aceptación, casos borde) antes de tocar código
- Usar la spec como fuente de verdad para generar: tests de aceptación, contratos OpenAPI/AsyncAPI, y el plan de PRs
- Diferencia práctica entre spec-first y "vibe coding": la spec se revisa y aprueba antes de implementar
- Plantilla de spec incluida en la skill `open-net-teacher`

### 3.2 Testing automatizado en profundidad
- Unit tests (xUnit + FluentAssertions + NSubstitute/Moq)
- Integration tests con **Testcontainers** (SQL Server / Cosmos emulator en Docker)
- Contract testing entre frontend y backend (o entre microservicios) con Pact o snapshot de OpenAPI
- Test de arquitectura (ArchUnitNET) para forzar reglas de capas automáticamente
- Cobertura como señal, no como meta (evitar tests "de relleno")

**Cubre de la oferta:** "automated testing practices", "engineering standards, best practices".

**Entregable:** cada feature del MVP nace con su spec en `/specs`, y no se mergea sin tests unitarios + al menos un test de integración.

---

## Fase 4 — CI/CD, IaC y despliegue en Azure (mes 6-9)

**Objetivo de negocio:** ser dueño del ciclo completo, no solo del código.

### 4.1 CI/CD
- Pipelines en **Azure DevOps** (mencionado explícito en la oferta) y GitHub Actions como alternativa
- Build → test → análisis estático (SonarCloud/Roslyn analyzers) → publish → deploy
- Estrategias de despliegue: blue-green, canary, feature flags
- Gestión de secretos con **Azure Key Vault**

### 4.2 Infraestructura como código
- **Bicep** (nativo de Azure) para: App Service/Container Apps, Azure SQL, Cosmos DB, Service Bus, Key Vault, API Management
- Ambientes: dev / staging / prod con pipelines separados y aprobaciones manuales en prod

### 4.3 Observabilidad y operación
- Application Insights + OpenTelemetry (traces distribuidos entre microservicios)
- Health checks (`/health`, `/ready`) y dashboards básicos
- Logging estructurado (Serilog) correlacionado por `TraceId`

### 4.4 Frontend deploy
- React + Vite desplegado en **Azure Static Web Apps**, consumiendo las APIs vía **Azure API Management** o un API Gateway propio (YARP)

**Cubre de la oferta:** "Azure DevOps", "CI/CD pipelines", "Microsoft Azure technologies", experiencia hands-on con infraestructura real.

**Entregable:** el MVP completo desplegado en Azure con pipeline automatizado desde `main` hasta producción.

---

## Fase 5 — Liderazgo técnico y extras (continuo)

No es "una fase" sino una práctica constante desde el día 1 del proyecto:

- **Mentoría / code review**: escribe PRs como si alguien junior los fuera a leer y aprender de ellos. Usa la skill para simular reviews exigentes.
- **ADRs (Architecture Decision Records)**: documenta cada decisión importante del MVP (por qué Cosmos y no todo en SQL, por qué Service Bus y no HTTP directo, etc.)
- **Certificaciones Azure** (bonus explícito): **AZ-204 (Azure Developer Associate)** primero, luego **AZ-305 (Solutions Architect Expert)** si quieres apuntar más alto que "Senior"
- **Dominio de React a nivel de colaboración** (bonus explícito): no necesitas ser frontend senior, pero sí entender componentes, estado, fetching de datos y cómo consumir tus propias APIs desde el cliente — por eso el proyecto es full-stack
- **Producto B2C / marketplace** (bonus explícito): el MVP propuesto simula justamente un marketplace, para que puedas hablar con propiedad de matching, catálogos y transacciones

---

## Checklist final antes de aplicar a roles Senior como este

- [ ] Puedo justificar cada patrón de diseño usado en mi proyecto sin decir "porque sí"
- [ ] Tengo al menos un sistema con comunicación asíncrona entre 2+ servicios funcionando en Azure real
- [ ] Tengo pipelines de CI/CD corriendo tests automáticos antes de cada deploy
- [ ] Puedo explicar el trade-off SQL vs Cosmos DB con un ejemplo propio
- [ ] Tengo specs escritas (SDD) para al menos 3 features del proyecto
- [ ] Puedo dibujar de memoria la arquitectura completa del MVP y explicar cada flecha
- [ ] (Opcional pero fuerte) AZ-204 aprobado

---

## Siguiente paso

1. Lee `proyecto-mvp-monorepo.md` para el proyecto que materializa este roadmap.
2. Activa/usa la skill `open-net-teacher` en opencode: ella te guiará fase por fase, exigiéndote spec antes de código, revisando tus PRs con rigor de senior, y sin dejarte saltar a la siguiente fase sin cumplir la "definition of done".
