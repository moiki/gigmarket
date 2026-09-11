# Referencia: Testing y Spec-Driven Development

Úsala cuando el usuario escriba tests, pida revisión de cobertura, o cuando toque convertir una spec en tests concretos.

## De spec a tests — el flujo que debes forzar

Cada criterio de aceptación de la spec (`specs/NNNN-*.md`) se convierte en al menos un test. Ejemplo:

```
Spec:
- [ ] Dado un precio negativo, cuando se crea un gig, entonces se rechaza con error de validación.

Test (xUnit + FluentAssertions):
[Fact]
public void CreateGig_WithNegativePrice_ShouldFail()
{
    var command = new CreateGigCommand(Title: "Clases", Price: -10);
    var result = await handler.Handle(command, default);
    result.IsSuccess.Should().BeFalse();
    result.Error.Should().Contain("price");
}
```

Si el usuario escribe código antes que el test correspondiente al criterio de aceptación, recuérdale volver y cerrar ese ciclo — no es obligatorio TDD estricto (rojo-verde-refactor) para cada línea, pero sí es obligatorio que cada criterio de la spec termine con un test que lo verifique antes de dar la feature por terminada.

## Pirámide de tests para este proyecto

```
        /\
       /  \      E2E (pocos) - Playwright contra el front real, opcional en MVP
      /----\
     /      \    Integration (moderados) - Testcontainers: API real + SQL/Cosmos real en Docker
    /--------\
   /          \  Unit (muchos) - Handlers de MediatR, lógica de dominio, validators
  /____________\
```

**Señal de alerta en review:** si el usuario solo tiene unit tests mockeando todo (incluida la base de datos) y ningún integration test, no está probando que EF Core/Cosmos SDK realmente funcionen como espera — falta la capa de integración.

## Testcontainers — setup base

```csharp
public class OrdersApiFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder().Build();

    public async Task InitializeAsync() => await _sqlContainer.StartAsync();
    public string ConnectionString => _sqlContainer.GetConnectionString();
    public async Task DisposeAsync() => await _sqlContainer.DisposeAsync();
}
```

Para Cosmos DB, usar el **Cosmos DB Emulator** vía contenedor Docker (Linux emulator image) en vez del SDK contra Azure real en tests.

## Test de arquitectura (ArchUnitNET) — automatizar la Fase 1

Ejemplo de regla que hace cumplir Clean Architecture sin depender de revisión manual:

```csharp
[Fact]
public void Domain_Should_Not_DependOn_Infrastructure()
{
    var result = Types().That().ResideInNamespace("Catalog.Domain")
        .Should().NotDependOnAny(Types().That().ResideInNamespace("Catalog.Infrastructure"))
        .GetResult();

    result.IsSuccessful.Should().BeTrue();
}
```

Sugiérele al usuario agregar esto en Fase 3 — convierte una regla que antes solo vivía "en la cabeza del senior" en algo que el CI verifica solo.

## Checklist de code review enfocado en testing

- [ ] ¿Cada criterio de aceptación de la spec tiene un test que lo cubre?
- [ ] ¿Hay al menos un test de integración que golpee la base de datos real (via Testcontainers)?
- [ ] ¿Los tests unitarios prueban comportamiento (input→output), no implementación interna?
- [ ] ¿Los nombres de test siguen un patrón legible? (`Metodo_Escenario_ResultadoEsperado`)
- [ ] ¿Los casos borde de la spec (no solo el happy path) están cubiertos?
- [ ] ¿Hay tests "falsos" que no fallarían aunque el código estuviera roto? (anti-pattern a señalar sin piedad)

## Anti-patrón de "vibe coding" a corregir activamente

Si el usuario pide "impleméntame la feature X" sin spec previa, tu respuesta debe ser redirigir a escribir la spec primero (ver plantilla en el SKILL.md principal), no ceder y generar código. Esta es la disciplina central de SDD que la oferta valora indirectamente al pedir "engineering standards" y "best practices".
