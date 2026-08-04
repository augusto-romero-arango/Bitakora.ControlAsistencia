---
fecha: 2026-04-06
hora: 18:00
sesion: diseno-arquitectonico
tema: Estrategia de integration testing - smoke tests contra entorno dev
---

## Contexto

Los unit tests del proyecto (ADR-0006) verifican logica de dominio con un event store en memoria (TestStore). Todo pasa, pero no habia forma de saber si lo desplegado realmente funciona: persistencia en PostgreSQL, serializacion JSON via Marten, validacion end-to-end, ni el pipeline completo Wolverine.

Se inicio una sesion de investigacion profunda para definir la estrategia de integration testing del proyecto.

## Alternativas evaluadas

### Testcontainers + HostBuilder (descartado)
- Construir un IHost con los mismos servicios que Program.cs contra un PostgreSQL en container Docker
- **Descartado por experiencia previa**: fragiles, mucha dependencia de configuracion, acoplan la prueba a la implementacion (connection strings, schemas, configuracion de Wolverine/Marten)

### .NET Aspire Testing (descartado)
- DistributedApplicationTestingBuilder para levantar toda la app distribuida
- **Descartado**: Microsoft reconoce que testing es "su mayor brecha" en Aspire. Inmaduro.

### Service Bus Emulator (descartado)
- **Descartado**: experimental y bajo ROI. La publicacion de eventos es responsabilidad de Wolverine/Cosmos, no del dominio.

### Smoke tests con HttpClient contra dev (elegido)
- Tests black-box que llaman a los endpoints HTTP reales del entorno dev
- Cero dependencias nuevas (HttpClient, xUnit v3, AwesomeAssertions)
- Completamente desacoplados de la implementacion
- Sin Docker, sin emuladores, sin containers

## Decisiones tomadas

### 1. Smoke tests black-box contra entorno real

- Proyecto separado: `tests/Bitakora.ControlAsistencia.{Dominio}.SmokeTests/`
- Sin ProjectReference a produccion — payloads como objetos anonimos
- Configuracion por jerarquia: appsettings.json < appsettings.local.json < env vars
- Aislamiento via `Guid.CreateVersion7()` — no se necesita cleanup
- ApiFixture con health check fail-fast como IAssemblyFixture
- Trait `[Trait("Category", "Smoke")]` para filtrar en CI

### 2. Integracion en CI/CD

- El workflow de deploy ahora tiene 3 jobs: `build-and-test` -> `deploy` -> `smoke-tests`
- Unit tests filtran `Category!=Smoke` para no intentar ejecutar smoke tests sin entorno
- Workflow reutilizable `smoke-tests.yml` con `workflow_dispatch` para ejecucion manual
- Se puede especificar la URL base desde GitHub Actions

### 3. Separacion de responsabilidades en agentes

**Insight clave**: la creacion de la estructura del proyecto y la escritura de tests son dos responsabilidades distintas.

- **domain-scaffolder** actualizado: ahora crea el proyecto `.SmokeTests/` como parte del scaffold (Paso 2b), incluyendo csproj, fixtures, appsettings, y el job de smoke tests en el workflow de deploy
- **smoke-test-writer** creado: agente nuevo que solo escribe tests dentro del proyecto ya existente. Lee endpoints del dominio, construye payloads anonimos, ejecuta contra dev

### 4. ADR-0016

Se documento la decision completa en `docs/adr/0016-smoke-tests-contra-entorno-dev.md`, incluyendo alternativas descartadas, estructura, CI/CD e integracion con agentes.

## Artefactos producidos

- `tests/Bitakora.ControlAsistencia.Programacion.SmokeTests/` — proyecto completo con 4 smoke tests pasando
- `.github/workflows/smoke-tests.yml` — workflow reutilizable con workflow_dispatch
- `.github/workflows/deploy-programacion.yml` — actualizado con 3 jobs y filtro Category!=Smoke
- `.github/workflows/ci.yml` — actualizado con filtro Category!=Smoke
- `.claude/agents/smoke-test-writer.md` — agente para escribir smoke tests
- `.claude/agents/domain-scaffolder.md` — actualizado con Paso 2b (proyecto SmokeTests)
- `docs/adr/0016-smoke-tests-contra-entorno-dev.md` — ADR de la decision

## Validacion

- 4/4 smoke tests pasando contra dev real (35s)
- 24/24 unit tests siguen pasando
- Build de la solucion completa: 0 errores, 0 warnings

## Aprendizajes

- **Testcontainers no son para todos**: la experiencia previa demostro que el acoplamiento a configuracion de infraestructura los hace fragiles. Para un proyecto serverless con Azure Functions, testear contra el entorno real es mas pragmatico y da mas confianza.
- **La simplicidad gana**: HttpClient puro + xUnit + assertions fluidas es todo lo que se necesita. Sin RestAssured, sin Playwright, sin librerias adicionales.
- **Separar scaffold de contenido**: el scaffolder crea la estructura, el writer crea el contenido. Mezclarlos produce agentes con dos responsabilidades.
