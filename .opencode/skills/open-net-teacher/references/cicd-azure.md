# Referencia: CI/CD, IaC y Despliegue en Azure

Úsala cuando el usuario trabaje en pipelines, Bicep, observabilidad o el despliegue final del MVP.

## Estructura de pipeline recomendada (Fase 4)

```
CI (en cada PR):
  1. Restore + Build (.NET) / Install (npm)
  2. Lint / análisis estático (Roslyn analyzers, ESLint)
  3. Unit tests
  4. Integration tests (Testcontainers en el runner)
  5. Build de imagen Docker (si aplica) o publish artifact
  6. SonarCloud/análisis de calidad (opcional pero valorado en la oferta)

CD (en merge a main, por ambiente):
  dev   → deploy automático
  staging → deploy automático + smoke tests
  prod  → requiere aprobación manual (gate)
```

**Path filters en monorepo:** el pipeline de `catalog-api` solo debe correr si cambió algo en `services/catalog-api/**` o `libs/**` — evita builds innecesarios y enseña una práctica real de monorepos grandes.

## Ejemplo mínimo GitHub Actions (adaptable a Azure DevOps)

```yaml
name: ci-catalog-api
on:
  pull_request:
    paths:
      - 'services/catalog-api/**'
      - 'libs/**'

jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet restore services/catalog-api
      - run: dotnet build services/catalog-api --no-restore
      - run: dotnet test services/catalog-api --no-build
```

Si el usuario tiene acceso a Azure DevOps (mencionado explícitamente en la oferta), ofrécele también el equivalente en `azure-pipelines.yml` con `stages` para build/test/deploy — es más representativo del stack real que usará Franki.

## Bicep — organización modular

```bicep
// main.bicep
module sql 'modules/sql.bicep' = { name: 'sql', params: { ... } }
module cosmos 'modules/cosmos.bicep' = { name: 'cosmos', params: { ... } }
module serviceBus 'modules/service-bus.bicep' = { name: 'sb', params: { ... } }
module containerApps 'modules/container-apps.bicep' = { name: 'apps', params: { ... } }
```

Enséñale a parametrizar por ambiente con `.bicepparam` (dev/prod) en vez de duplicar archivos `.bicep` — un error común de nivel junior/mid es copiar-pegar el bicep completo por ambiente.

**Principio a reforzar:** todo recurso de Azure del proyecto debe nacer en código (Bicep), nunca creado a mano en el portal — eso es lo que la oferta llama "cloud-native applications" en la práctica.

## Gestión de secretos e identidad

- **Azure Key Vault** para connection strings, API keys.
- **Managed Identity** en Container Apps/Functions para acceder a Key Vault, SQL, Cosmos y Service Bus **sin secretos en config** — este es un salto de calidad que distingue a un senior: nadie debería ver una connection string en `appsettings.json` en el repo.

## Observabilidad end-to-end

- Application Insights conectado a los 3 servicios (`catalog-api`, `orders-api`, `notifications-worker`)
- OpenTelemetry para propagar `TraceId` entre el request HTTP inicial y el mensaje de Service Bus que dispara la Function — esto permite ver en Application Insights el flujo completo "usuario crea orden → evento → notificación enviada" como un solo trace distribuido.
- Serilog con enrichers para loguear siempre `TraceId`, `UserId` (si aplica), nombre del servicio.

## Checklist antes de considerar el MVP "desplegado en Azure" de verdad

- [ ] Los 3 servicios corren en Azure real (no solo localhost/Docker Compose)
- [ ] El frontend en Static Web Apps consume las APIs vía APIM, no URLs hardcodeadas de Container Apps
- [ ] Ningún secreto está en el repo ni en variables de entorno planas — todo pasa por Key Vault + Managed Identity
- [ ] Hay al menos un pipeline de CI/CD que se disparó automáticamente y desplegó sin intervención manual (excepto el gate de prod)
- [ ] Application Insights muestra al menos un trace distribuido completo del flujo "crear orden → notificación"
- [ ] Existe un `README.md` con diagrama de arquitectura y pasos para levantar el entorno local con `docker-compose up`

Cuando todo esto esté marcado, el usuario tiene un proyecto defendible en una entrevista técnica de nivel Senior para un rol como el de Franki.
