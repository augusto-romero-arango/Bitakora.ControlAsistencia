---
fecha: 2026-07-18
hora: 19:05
sesion: bug-investigator
tema: Smoke tests fallan con HTTP 500 en ambos dominios tras upgrade Cosmos.Event* 2.1.0 (ITenantResolver no registrado)
---

## Sintoma reportado

Los smoke tests de los ultimos deploys fallan en AMBOS dominios (build y deploy pasan; falla solo el step "Smoke tests"):

- **ControlHoras** (run 29665739185, PR #218 / issue 213): 9 de 11 fallan. HTTP 500 donde se esperaba 202/400/404/409 en `RegistrarMarcacion`. Ademas la cadena Service Bus falla: el listener `AsignarTurno` no persiste `turno_diario_asignado` (TimeoutException 120s).
- **Programacion** (run 29664912897, PR #217 / issue 210): 10 de 11 fallan (fallo rapido, 10s). HTTP 500 en `SolicitarProgramacionTurno` y `CrearTurno`, incluso en casos de validacion (body/guid vacio) que deberian cortar en 400 sin tocar dependencias.

Pista del reporter: que hasta los casos de validacion devuelvan 500 apunta a fallo de host/arranque/DI/middleware, no a logica de negocio.

## Investigacion

### App Insights (controlasistencias-dev-ai / rg-controlasistencias-dev)

Telemetria fluye correctamente (descarta que App Insights o el host esten caidos). Desglose de excepciones ultimas 24h:

- 261  `Microsoft.Azure.WebJobs.Script.Workers.Rpc.RpcException` (envoltorio de la invocacion fallida)
- 200  `System.InvalidOperationException`: **"Unable to resolve service for type 'Cosmos.MultiTenancy.ITenantResolver' while attempting to activate 'Cosmos.EventSourcing.CritterStack.Commands.WolverineCommandRouter'"** (y la variante `WolverinePrivateEventRouter`)
- 5    `System.TimeoutException` "Exception while reading from stream" (Postgres, transitorio)
- 3    `Npgsql.NpgsqlException` "Exception while reading from stream" (Postgres, transitorio)

CERO excepciones de KeyVault, storage/identidad administrada, o Service Bus (MessagingEntityNotFound / Unauthorized). Eso descarta las hipotesis de infra (#197 storage MI, #196/#195 Key Vault, #212 topic, #206 App Insights).

Stack de una muestra (rol func-asist-dev-programacion, op CrearTurno):
`DefaultFunctionActivator.CreateInstance` -> se activa la clase de la funcion -> su constructor inyecta `WolverineCommandRouter` -> el ctor del router exige `ITenantResolver` -> no registrado -> InvalidOperationException -> RpcException -> HTTP 500. Falla ANTES de ejecutar cualquier codigo de la funcion, por eso hasta los casos 400 devuelven 500.

### Correlacion con el codigo desplegado (origin/main)

- `git fetch`: origin/main esta 3 commits adelante del checkout local. HEAD deployado = 22b2e41 (merge #218).
- `Program.cs` de ambos dominios llama a `AgregarWolverineCommandRouter()`, y ControlHoras ademas a `AgregarWolverinePrivateEventRouter()`. **Ninguno registra un `ITenantResolver`.**
- csproj: ambos dominios en `Cosmos.Event* 2.1.0`. Ninguno referencia `Cosmos.MultiTenancy.CritterStack` directamente.

### Decompilacion de los building blocks (ilspycmd) - prueba de la regresion

Version anterior **0.1.9** (`Cosmos.EventSourcing.CritterStack`), metodo de extension:
```
public void AgregarWolverineCommandRouter()
{
    serviceCollection.AddScoped<ITenantResolver, TenantResolver>();   // <-- registraba resolver por defecto
    serviceCollection.AddScoped<ICommandRouter, WolverineCommandRouter>();
}
```

Version desplegada **2.1.0**, mismo metodo:
```
public void AgregarWolverineCommandRouter()
{
    serviceCollection.AddScoped<ICommandRouter, WolverineCommandRouter>();   // <-- registro de ITenantResolver ELIMINADO
}
```

El constructor `WolverineCommandRouter(IMessageBus, ITenantResolver)` no cambio (ya exigia el resolver en 0.1.9). Lo que cambio en 2.x es que **el metodo de extension dejo de auto-registrar el `TenantResolver` por defecto**. Igual para `AgregarWolverinePrivateEventRouter()` (`WolverinePrivateEventRouter(IMessageBus, ITenantResolver)`) y `AgregarWolverineQueryRouter()`.

En 2.1.0 las implementaciones y el registro de `ITenantResolver` se movieron a paquetes separados:
- `Cosmos.MultiTenancy.CritterStack`: `ProxyTenantResolver(IMessageContext, IHttpContextAccessor)` via `AgregarTenantResolverHibrido()`; `WolverineMessageContextTenantResolver(IMessageContext)`.
- `Cosmos.MultiTenancy.AspNetCore`: `TrustedHeadersTenantResolver(IHttpContextAccessor)` via `AgregarTenantResolverConHeadersConfiables()`.
- `Cosmos.MultiTenancy` (base 2.1.0): solo la interfaz, sin impl por defecto ni extension DI.

El commit de #207 (affa2e0) lo dice sin saberlo: "el salto doble-mayor no rompio la superficie de API consumida: compila en Release sin cambios y la suite de tests sigue verde". Compila porque la firma publica de `AgregarWolverineCommandRouter()` no cambio; los tests unitarios no construyen el grafo DI del host de Functions, asi que no activan el router. El fallo solo aparece en runtime en el Function App desplegado, que es justo lo que los smoke tests (CA-5/CA-6 del propio issue) detectaron.

## Diagnostico

**Causa raiz (confianza alta, confirmada):** el upgrade de building blocks Cosmos.Event* de 0.1.9 a 2.1.0 (issue #207, commit affa2e0) es un cambio breaking no detectado. En 2.x, `AgregarWolverineCommandRouter()` / `AgregarWolverinePrivateEventRouter()` dejaron de registrar un `ITenantResolver` por defecto (ese registro se movio a `Cosmos.MultiTenancy.CritterStack`), pero el constructor de los routers lo sigue exigiendo. Como el `Program.cs` de ambos dominios nunca registra un `ITenantResolver`, TODA activacion de una funcion HTTP (que inyecta el command router) o del handler de evento privado (private event router) lanza `InvalidOperationException` -> HTTP 500 / RpcException.

Un unico defecto explica los dos sintomas:
- **HTTP 500 en todos los endpoints** (RegistrarMarcacion, SolicitarProgramacionTurno, CrearTurno), incluidos los casos de validacion: el fallo es al construir la clase de la funcion, antes de validar.
- **Cadena Service Bus rota en ControlHoras**: `AsignarTurnoCuandoProgramacionTurnoDiarioSolicitada` usa el private event router, que tampoco se puede activar -> el evento nunca se procesa -> `turno_diario_asignado` no se persiste -> timeout de 120s en el smoke.

Hipotesis descartadas con evidencia:
- Infra (Key Vault #195/#196, storage con identidad administrada #197, topic marcacion-registrada #212, App Insights fuera de KV #206): 0 excepciones de auth/conexion/entidad; la telemetria fluye. No son la causa.
- Deploy/hosting (ADR-0020, plan Y1, settings faltantes): host arranca y responde; falla la activacion por invocacion, no el arranque.
- Noisy-neighbor por plan compartido (#43): el 500 es determinista con excepcion clara, no timeouts intermitentes sin excepcion. No aplica.

Observacion secundaria (baja prioridad): 8 excepciones transitorias de Postgres ("Exception while reading from stream") en 24h, probablemente del agente de durabilidad de Wolverine (Solo) ante blips de conexion. Monitorear; no bloquea los smoke.

## Acciones

Propuestas (pendientes de confirmacion del usuario; el bug-investigator no modifica codigo):

1. **Fix de wiring (tipo:refactor, dom:controlhoras + dom:programacion)** — registrar un `ITenantResolver` en el `Program.cs` de ambos dominios acorde a 2.x. Opcion natural (cubre contexto HTTP + Wolverine): referenciar `Cosmos.MultiTenancy.CritterStack` 2.1.0 y agregar `builder.Services.AddHttpContextAccessor();` + `builder.Services.AgregarTenantResolverHibrido();` (ProxyTenantResolver). Validar contra la guia de migracion 2.x del building block cual resolver corresponde a la estrategia de tenancy del proyecto (el dominio es efectivamente mono-tenant). Evaluar nota/ADR sobre estrategia de tenancy.

2. **Mitigacion inmediata para desbloquear dev**: rollback de Cosmos.Event* a 0.1.9 (restaura el auto-registro del resolver y deja verdes los smoke) mientras se implementa el wiring correcto de 2.x. Alternativa mas rapida si se necesita dev operativo ya.

3. **Guardrail de proceso (tipo:tooling)**: el pipeline valido "compila + tests verdes" pero no la composicion del DI del host. Considerar un test que construya el `IHost`/`FunctionsApplication` y resuelva `ICommandRouter`/`IPrivateEventRouter` (o un smoke minimo obligatorio pre-merge) para atrapar breaking changes de wiring en futuros upgrades de building blocks.

Ningun workaround ejecutado (no se purgo cola ni se reinicio funcion).

## Preguntas abiertas

- Estrategia de tenancy del proyecto: cual de los resolvers de 2.x corresponde (hibrido/ProxyTenantResolver vs message-context vs headers confiables)? Requiere la guia de migracion 2.1.0 del harness de building blocks.
- Con `ConfigureFunctionsWebApplication` (isolated + ASP.NET Core), `IHttpContextAccessor` esta disponible para `ProxyTenantResolver` en el contexto HTTP? Verificar al implementar.
- Confirmar si prod corre estas versiones; si es asi, el mismo fallo aplicaria alli.
- Las 8 excepciones transitorias de Postgres: vigilar si escalan tras el fix (posible contencion del agente de durabilidad).
