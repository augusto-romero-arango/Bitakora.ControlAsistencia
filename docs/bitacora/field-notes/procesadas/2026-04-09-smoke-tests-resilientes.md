---
fecha: 2026-04-09
hora: 13:30-19:30
sesion: implementacion-y-correccion
tema: Fixtures resilientes para smoke tests - issues #23, #24, #25
---

## Contexto

Los smoke tests creados en la sesion del 2026-04-06 (ADR-0016) solo verificaban HTTP. Al agregar
verificacion de Service Bus y Postgres (issue #18), 14 de 14 tests del assembly fallaban cuando
el connection string no estaba configurado. La causa: un `?? throw` en el `InitializeAsync` de
`ServiceBusFixture` registrado como `[assembly: AssemblyFixture]` — xUnit cancela todo el assembly
si un fixture lanza en inicializacion.

## Problema central: cascading failure de AssemblyFixture

Un solo fixture que lanza = todos los tests del assembly muertos. No hay forma de saber cual test
realmente necesitaba ese fixture. Esto es un defecto de diseno de xUnit v3 AssemblyFixture: no hay
skip granular a nivel de fixture.

## Solucion: patron IsConfigured + Assert.SkipWhen

En vez de lanzar excepcion, cada fixture expone `bool IsConfigured`:

```csharp
var connectionString = configuration["ServiceBus:ConnectionString"];
if (string.IsNullOrWhiteSpace(connectionString))
{
    IsConfigured = false;
    return ValueTask.CompletedTask;
}
IsConfigured = true;
```

Y cada test decide si se omite:

```csharp
Assert.SkipWhen(!serviceBus.IsConfigured,
    "ServiceBus no configurado...");
```

**Importante**: es `Assert.SkipWhen()` de xUnit v3. `Skip.When()` NO existe y no compila — caimos
en este error durante la sesion.

## Trampa con Assert.SkipWhen y parametro reason

`Assert.SkipWhen(condition, reason)` valida el parametro `reason` SIEMPRE, incluso cuando condition
es false. Si pasas `postgres.SkipReason!` y SkipReason es null (porque IsConfigured es true),
lanza `ArgumentNullException`. Fix: `postgres.SkipReason ?? "Postgres no disponible."`.

## PostgresFixture: firewall de Azure

La conexion a Postgres desde desarrollo local requiere IP whitelisted en Azure. Sin whitelist, el
fixture se queda colgado hasta timeout. Solucion: catch de `NpgsqlException` cuando `InnerException`
es `SocketException` o `TimeoutException`, con mensaje descriptivo en `SkipReason`.

## Polling tolerante a excepciones

El smoke test de ControlHoras fallaba intermitentemente porque la query SQL a `mt_events` fallaba
cuando el schema de Marten aun no existia (primera ejecucion). Solucion: try/catch dentro del loop
de polling, acumular `lastException`, reportar en `TimeoutException` al agotar el timeout.

## Gestion de secrets: decision y reversal

Secuencia de decisiones:
1. Inicialmente se quiso usar solo variables de entorno (no quemar secrets en ningun archivo)
2. Se evaluo .env + dotenv, Azure Key Vault references, User Secrets
3. **Decision final**: jerarquia estandar de .NET. `appsettings.json` commiteado con placeholders
   vacios + `appsettings.local.json` gitignored con valores reales + env vars en CI. El argumento:
   "Es el mismo riesgo tener un .env que tenerlo en settings."

## Pipeline tooling #24: fallo silencioso

El pipeline de tooling para issue #24 fallo porque el writer no produjo cambios. Causa raiz doble:

1. **Modelo flaky**: el agente pidio permisos para escribir a `.claude/agents/domain-scaffolder.md`
   en vez de escribir directamente, a pesar de `--permission-mode bypassPermissions`. El issue #25
   (mismo modo, mismos paths) funciono sin problema. Comportamiento no deterministico.

2. **Bug en deteccion de cambios**: el pipeline parchea `.claude/settings.json` antes de invocar al
   agente (para rutas absolutas en hooks). `git status --porcelain` sin filtrar siempre detectaba
   este cambio, haciendo que el check "el writer no genero ningun cambio" nunca abortara. El
   pipeline continuaba al reviewer, gastando otro agente, y solo fallaba al final con "no hay commits".

Fixes aplicados:
- Check de cambios ahora restaura `settings.json` y filtra por paths significativos
- Prompts de writer y reviewer declaran explicitamente: "Tienes permisos completos... No pidas
  permisos ni confirmacion"

## Issues completados en esta sesion

| Issue | Titulo | Resultado |
|-------|--------|-----------|
| #23 | Estandarizar fixtures de smoke tests | PR #26 mergeado |
| #24 | Actualizar domain-scaffolder para fixtures | PR #28 creado |
| #25 | Actualizar smoke-test-writer y reviewer | PR #27 mergeado (via pipeline) |

## Lecciones

- xUnit v3 `AssemblyFixture` es una bomba si el fixture lanza: mata todo el assembly. Los fixtures
  compartidos deben ser resilientes por diseno (IsConfigured, no throw).
- Los prompts de agentes en pipelines no-interactivos necesitan ser explicitos sobre permisos.
  "bypassPermissions" no es suficiente — el modelo puede decidir ser cauteloso de todos modos.
- Los checks de validacion en scripts deben filtrar por paths relevantes. Un archivo auxiliar
  modificado por el propio pipeline puede enmascarar la ausencia de trabajo real.
- ADR-0016 actualizado para reflejar la evolucion de smoke tests HTTP-only a verificacion completa
  de Service Bus y Postgres.
