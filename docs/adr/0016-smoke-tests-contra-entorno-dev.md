# ADR-0016: Smoke tests contra entorno dev desplegado

## Estado

Aceptado

## Contexto

Los unit tests (ADR-0006) verifican logica de dominio con un event store en memoria (TestStore). Esto
cubre correctamente la logica de negocio pero no verifica que el sistema desplegado funcione: que la
Function App responda, que la persistencia en PostgreSQL via Marten funcione, que la serializacion JSON
sea correcta, ni que la validacion opere end-to-end.

Se evaluaron alternativas:

- **Testcontainers**: descartado por experiencia previa en otro proyecto. Son fragiles, requieren mucha
  configuracion y acoplan la prueba a la implementacion (connection strings, schemas, configuracion de
  Wolverine/Marten).
- **.NET Aspire Testing**: inmaduro. Microsoft reconoce que testing es su mayor brecha en Aspire.
- **Service Bus Emulator**: experimental y bajo ROI para verificar publicacion de eventos.

Se necesita un enfoque que verifique "esto realmente funciona" con minima sobrecarga.

## Decision

Se adoptan smoke tests con HttpClient puro contra el entorno dev desplegado. Los tests son black-box:
llaman a los endpoints HTTP reales y verifican status codes. No tienen dependencia de la implementacion
interna.

### Estructura

```
tests/Bitakora.ControlAsistencia.Programacion.SmokeTests/
  appsettings.json              -- URL del entorno (commiteado)
  appsettings.local.json        -- overrides locales (gitignored)
  Fixtures/ApiFixture.cs        -- HttpClient + health check fail-fast
  CrearTurnoFunction/           -- tests por feature
```

### Configuracion

Jerarquia estandar de .NET: `appsettings.json` < `appsettings.local.json` < variables de entorno
(`Api__BaseUrl`). Permite ejecutar contra dev, staging o localhost sin cambiar codigo.

### Aislamiento de datos

Cada test genera un TurnoId con `Guid.CreateVersion7()`. No hay interferencia entre ejecuciones ni
necesidad de cleanup.

### Ejecucion

```bash
dotnet test --project tests/Bitakora.ControlAsistencia.Programacion.SmokeTests/
dotnet test --filter "Category=Smoke"                    # desde la raiz
Api__BaseUrl=http://localhost:7071 dotnet test ...       # contra local
```

### Integracion en el proceso de desarrollo

La infraestructura del proyecto de smoke tests (csproj, fixtures, appsettings, workflow) la crea el
`domain-scaffolder` como parte del scaffold de cada nuevo dominio. Los tests los escribe el agente
`smoke-test-writer`, que asume que el proyecto ya existe y se limita a escribir tests black-box.

Responsabilidades separadas:
- **domain-scaffolder**: crea `tests/*.SmokeTests/` con ApiFixture, AssemblyFixture, appsettings.json,
  csproj sin ProjectReference a produccion, y el job `smoke-tests` en el workflow de deploy.
- **smoke-test-writer**: escribe tests dentro de ese proyecto. Lee los endpoints del dominio, construye
  payloads como objetos anonimos (sin referenciar clases de produccion), y ejecuta contra dev.

### CI/CD

El workflow de deploy de cada dominio tiene tres jobs:

```
build-and-test (unit tests, --filter "Category!=Smoke") -> deploy -> smoke-tests
```

Los smoke tests tambien se pueden ejecutar manualmente desde GitHub Actions via el workflow reutilizable
`smoke-tests.yml` con `workflow_dispatch`, que permite especificar la URL base del entorno.

## Consecuencias

### Positivas

- **Verificacion real**: confirma que el sistema desplegado funciona end-to-end (HTTP -> validacion ->
  handler -> Marten -> PostgreSQL).
- **Cero acoplamiento**: los tests no conocen la implementacion. Si se cambia Marten por otro event
  store, los smoke tests siguen funcionando sin modificacion.
- **Cero infraestructura local**: no requiere Docker, emuladores ni containers. Solo un entorno
  desplegado.
- **Integracion natural en CI/CD**: se ejecutan como job post-deploy en GitHub Actions.

### Negativas

- **Dependencia del entorno**: si dev esta caido, los tests fallan. El fixture mitiga esto con un
  health check fail-fast que da un mensaje claro.
- **Datos residuales**: cada ejecucion crea turnos en la base de datos de dev. Al ser GUIDs unicos y
  tener prefijo `[TEST]`, no interfieren con datos reales, pero se acumulan.
- **No cubren todo**: no verifican publicacion de eventos a Service Bus ni consumo de mensajes. Para
  eso se necesitarian tests mas elaborados en el futuro.
