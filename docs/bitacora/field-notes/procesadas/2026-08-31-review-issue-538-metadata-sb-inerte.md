# Field Note: la metadata de tenant en Service Bus queda inerte hasta el flip

**Fecha**: 2026-08-31
**Issue**: #538 (adaptar los fixtures de smoke tests para declarar identidad de tenant)
**Etapa**: review (stage 2)
**Draft espejo en el marco**: harness#798

## Hecho verificado

El plano Service Bus de los fixtures ahora adjunta `ApplicationProperties["tenant-id"]` y
`["user_id"]`. Las dos claves son correctas -- verificadas por decompilacion (`ilspycmd`) de los
ensamblados que el proyecto resuelve hoy:

| Clave | Ensamblado | Evidencia |
|---|---|---|
| `tenant-id` | Wolverine 6.16.0 | `EnvelopeMapper<TIncoming,TOutgoing>` ctor: `MapPropertyToHeader(x => x.TenantId, "tenant-id")`; ademas `EnvelopeConstants.TenantIdKey = "tenant-id"`. `AzureServiceBusEnvelopeMapper.writeIncomingHeaders` copia cada `ApplicationProperty` a `Envelope.Headers` sin prefijo. |
| `user_id` | Cosmos.MultiTenancy.CritterStack 2.3.0 | `WolverineMessageContextTenantResolver.UserId` => `messageContext.Envelope.Headers.GetValueOrDefault("user_id")`, y lanza `InvalidOperationException` si falta. |

Version resuelta confirmada en `src/…Sedes/obj/project.assets.json`: `WolverineFx/6.16.0`,
`WolverineFx.AzureServiceBus/6.16.0`.

## Hallazgo: hoy nadie lee esas claves

El gate empirico del issue pedia verificar la clave de wire, y esta bien verificada. Pero verificar
la clave no era suficiente: **en este proyecto no existe el consumidor de esa clave.**

- Ningun `Program.cs` monta un listener de Wolverine sobre Azure Service Bus (cero `ListenTo…` /
  `UseAzureServiceBus` en `src/`). Por eso el `EnvelopeMapper` de Wolverine nunca corre en el camino
  de entrada.
- Cada evento entra por `[ServiceBusTrigger]` de Azure Functions. La clase base
  (`PrivateEventEndpointBase.ProcesarMensaje`) deserializa el `Body` y llama
  `IPrivateEventRouter.InvokeAsync(evento, ct)`. El `ServiceBusReceivedMessage` entrante -- y con el
  sus `ApplicationProperties` -- se descarta.
- `WolverinePrivateEventRouter` (Cosmos.EventDriven.CritterStack 2.3.1) arma
  `DeliveryOptions { TenantId = tenantResolver.TenantId }` + `.WithHeader("user_id", tenantResolver.UserId)`
  **desde el `ITenantResolver` del proceso**, no desde el mensaje entrante.

Consecuencia para el flip a etapa (b): si `/install-auth` cambia los 4 dominios a
`AgregarTenantResolverHibrido()`, el `ProxyTenantResolver` en un `[ServiceBusTrigger]` no tiene
`HttpContext`, asi que cae a `WolverineMessageContextTenantResolver(messageContext)` -- y ese
`IMessageContext` no proviene de un envelope entrante, asi que `TenantId` viene vacio y lanza.
**Declarar la metadata en el fixture no lo evita**, porque el puente
`ApplicationProperties` -> `IMessageContext` no existe.

## Lo que esto NO cambia

El cambio del issue #538 sigue siendo correcto y mergeable: es inocuo en etapa (a), las claves son
las correctas, y el plano HTTP (`X-Tenant-Id`/`X-User-Id`, CA-1) si es genuinamente
forward-compatible porque `TrustedHeadersTenantResolver` lee del `HttpContext` real. CA-2 queda
cumplido al pie de la letra; lo que falta es del otro lado del cable.

## Accion pendiente (no es de este issue)

`/install-auth` (o el issue que haga el flip) necesita, ANTES de cambiar el resolver, decidir como
el camino `[ServiceBusTrigger]` obtiene identidad de tenant. Opciones a evaluar alli:

1. Un `ITenantResolver` que lea del `ServiceBusReceivedMessage` del trigger (via
   `FunctionContext`/`IHttpContextAccessor` equivalente) -- consumiria las claves que este issue ya
   declara.
2. Que `PrivateEventEndpointBase` propague explicitamente `tenant-id`/`user_id` del mensaje entrante
   al `IPrivateEventRouter`, ampliando su firma.
3. Mantener `TenantResolverFijo` solo en el camino de eventos y usar el hibrido solo en HTTP.

La opcion (1) es la que hace util la metadata de este issue; conviene confirmarla antes de correr el
flip, no despues.

## Correcciones aplicadas en el review

- `IdentidadDePrueba` (record, uno por proyecto de smoke): unifica el tenant/usuario de prueba que
  antes estaba duplicado entre `ApiFixture` y `ServiceBusFixture`. Los dos planos deben declarar la
  misma identidad; tenerla en dos archivos permitia que se desincronizaran en silencio.
- Bug de configuracion: `configuration["Tenant:Id"] ?? PorDefecto` no caia al default con cadena
  vacia, y los appsettings de smoke usan cadena vacia como "sin configurar"
  (`ServiceBus:ConnectionString`, `Postgres:ConnectionString`). Con `Tenant:Id: ""` se mandaba un
  header vacio en vez del fallback. Corregido con `string.IsNullOrWhiteSpace`.
- Comentario del `ServiceBusFixture`: afirmaba implicitamente que
  `WolverineMessageContextTenantResolver` leeria estas claves. Reescrito con la advertencia de arriba.

## Leccion

Un gate de verificacion empirica sobre "el nombre de la clave" se cumple sin responder la pregunta
que importa: **quien la lee**. Verificar el productor (que clave escribo) y el consumidor (quien la
consume en ESTE codigo, no en el codigo que la libreria supone) son dos gates distintos. El segundo
es el que decide si un cambio "forward-compatible" realmente lo es.
