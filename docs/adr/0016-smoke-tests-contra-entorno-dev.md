# ADR-0016: Smoke tests contra entorno dev desplegado

## Estado

Aceptado (actualizado 2026-04-09: verificacion de Service Bus, Postgres y skip graceful)

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
tests/Bitakora.ControlAsistencia.{Dominio}.SmokeTests/
  appsettings.json              -- URL + placeholders vacios (commiteado)
  appsettings.local.json        -- cadenas reales (gitignored)
  Fixtures/
    ApiFixture.cs               -- HttpClient + health check fail-fast
    ServiceBusFixture.cs        -- PublishAsync + WaitForMessageAsync
    PostgresFixture.cs          -- consultas a Marten (mt_events)
    Polling.cs                  -- polling tolerante a excepciones
    AssemblyFixture.cs          -- registra los 3 fixtures
  CrearTurnoFunction/           -- tests por feature
```

### Fixtures obligatorios

En un sistema event-driven, todos los dominios publican y consumen eventos. Los tres fixtures
(Api, ServiceBus, Postgres) se generan siempre para todo dominio nuevo. No se pregunta al usuario
si el dominio los necesita — el scaffolder los crea y estan listos para usar desde el primer dia.

### Configuracion y secrets

Jerarquia estandar de .NET: `appsettings.json` < `appsettings.local.json` < variables de entorno.

- `appsettings.json` (commiteado): contiene la URL base y placeholders vacios para ServiceBus y
  Postgres connection strings. Nunca contiene valores reales.
- `appsettings.local.json` (gitignored): cadenas de conexion reales para desarrollo local.
- Variables de entorno en CI: `ServiceBus__ConnectionString`, `Postgres__ConnectionString`. Se pasan
  como secrets opcionales (`required: false`) en el workflow de deploy.

Esta jerarquia permite que los smoke tests se ejecuten en cualquier contexto (local, CI, manual)
sin cambiar codigo y sin exponer secrets en el repositorio.

### Aislamiento de datos

Cada test genera un TurnoId con `Guid.CreateVersion7()`. No hay interferencia entre ejecuciones ni
necesidad de cleanup.

### Ejecucion

```bash
dotnet test --project tests/Bitakora.ControlAsistencia.Programacion.SmokeTests/
dotnet test --filter "Category=Smoke"                    # desde la raiz
Api__BaseUrl=http://localhost:7071 dotnet test ...       # contra local
```

### Skip graceful: IsConfigured + Assert.SkipWhen

Los fixtures de ServiceBus y Postgres no lanzan excepcion si la configuracion no esta disponible.
En su lugar, exponen `bool IsConfigured` y los tests usan `Assert.SkipWhen` (xUnit v3) para
omitirse con un mensaje descriptivo:

```csharp
Assert.SkipWhen(!serviceBus.IsConfigured,
    "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
Assert.SkipWhen(!postgres.IsConfigured,
    postgres.SkipReason ?? "Postgres no disponible.");
```

Esto resuelve dos problemas:
- **AssemblyFixture cascading failure**: si un fixture lanza en `InitializeAsync`, xUnit cancela
  TODOS los tests del assembly. Con `IsConfigured`, el fixture se inicializa sin error y los tests
  individuales se omiten con un mensaje claro.
- **Firewall de Azure**: PostgresFixture atrapa `NpgsqlException` con `SocketException`/`TimeoutException`
  y expone `SkipReason` con instrucciones para agregar la IP al firewall.

**Importante**: es `Assert.SkipWhen()` de xUnit v3, NO `Skip.When()` que no existe y no compila.

### Polling tolerante a excepciones

El helper `Polling` captura excepciones transitorias dentro del loop de retry en vez de propagar al
primer error. Si el timeout se agota, reporta la ultima excepcion en el `TimeoutException`. Esto
maneja casos como tablas de Marten que aun no existen en la primera consulta.

### Integracion en el proceso de desarrollo

La infraestructura del proyecto de smoke tests (csproj, fixtures, appsettings, workflow) la crea el
`domain-scaffolder` como parte del scaffold de cada nuevo dominio. Los tests los escribe el agente
`smoke-test-writer`, que asume que el proyecto ya existe y se limita a escribir tests black-box.

Responsabilidades separadas:
- **domain-scaffolder**: crea `tests/*.SmokeTests/` con los 3 fixtures, Polling, appsettings.json
  con placeholders, csproj con ProjectReference a Contracts (para igualdad de records), y el job
  `smoke-tests` con secrets opcionales en el workflow de deploy.
- **smoke-test-writer**: escribe tests dentro de ese proyecto. Usa `Assert.SkipWhen` para tests
  que dependen de ServiceBus o Postgres. Puede verificar publicacion de eventos (WaitForMessageAsync)
  y persistencia en Marten (ExisteEventoAsync, ObtenerEventoAsync).

### CI/CD

El workflow de deploy de cada dominio tiene tres jobs:

```
build-and-test (unit tests, --filter "Category!=Smoke") -> deploy -> smoke-tests
```

El job `smoke-tests` pasa secrets opcionales (`required: false`) para ServiceBus y Postgres. Si los
secrets no estan configurados en el repo, los tests que dependen de ellos se omiten via `Assert.SkipWhen`
en vez de fallar. Esto permite que el pipeline funcione desde el primer deploy sin configuracion extra.

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

- **Dependencia del entorno**: si dev esta caido, los tests fallan. Los fixtures mitigan esto con
  health check fail-fast (Api) y skip graceful (ServiceBus, Postgres) con mensajes descriptivos.
- **Datos residuales**: cada ejecucion crea datos en la base de datos de dev. Al ser GUIDs unicos y
  tener prefijo `[TEST]`, no interfieren con datos reales, pero se acumulan.
- **Firewall de Azure**: las conexiones a Postgres desde desarrollo local requieren IP whitelisted
  en el portal de Azure. PostgresFixture detecta esto y omite los tests con un mensaje claro.
