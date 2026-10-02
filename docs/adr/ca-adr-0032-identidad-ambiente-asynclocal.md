# CA-ADR-0032: Identidad ambiente AsyncLocal que reemplaza a ProxyTenantContext

> **Nomenclatura (enmienda 2026-10-01, #699)**: este ADR usa los nombres de Cosmos.BuildingBlocks 3.x
> (`ITenantContext`, `TrustedHeadersTenantContext`, `WolverineMessageTenantContext`,
> `ProxyTenantContext`, `AgregarTenantContext*`). En 2.x se llamaban `ITenantResolver`,
> `TrustedHeadersTenantResolver`, `WolverineMessageContextTenantResolver`, `ProxyTenantResolver` y
> `AgregarTenantResolver*`. El analisis del incidente del 2026-09-01 (2.x) es igualmente valido con
> los nombres nuevos: el fallo del proxy es el mismo. La biblioteca local conserva el nombre
> `Bitakora.ControlAsistencia.TenantResolver`.

## Estado

Aceptado.

## Contexto

El merge de #545 (adopcion de WorkOS AuthKit + APIM, MEF-ADR-0032 del marco) migro los cuatro
dominios de la etapa (a) de MEF-ADR-0028 (`TenantResolverMonoTenantPorDefecto`, CA-ADR-0027) a la
etapa (b) que la seccion 4 de ese mismo ADR fija como transicion automatizada:
`services.AgregarTenantContextHibrido()` (`Cosmos.MultiTenancy.CritterStack`, registra
`ProxyTenantContext`). Tras el merge, los smoke tests quedaron en rojo y **todo** el trafico HTTP
de los 4 dominios fallo en dev: los comandos POST respondian 409 falsos y las queries GET 500.

Root cause, reproducido en vivo contra `func-asist-dev-sedes` (commit 66e7a80, 2026-09-01):
`ProxyTenantContext` decide su rama (`TrustedHeadersTenantContext` vs
`WolverineMessageTenantContext`) en su **constructor**, segun
`IHttpContextAccessor.HttpContext`. En el worker aislado de Azure Functions ese `HttpContext` es
`null` en el momento en que el grafo de DI lo construye -- toda request HTTP caia en la rama de
Wolverine y lanzaba `InvalidOperationException` ("El `IMessageContext` actual no tiene TenantId"),
que el catch de los `FunctionEndpoints` de comandos (CA-ADR-0030) convertia en un 409 enganoso; las
queries GET, sin ese catch, devolvian 500 sin traducir.

Este fallo **no es nuevo**: MEF-ADR-0028 seccion 4 fija la migracion (a)->(b) via `/install-apim`
como el auto-cableo de `AgregarTenantContextHibrido()` sobre todos los dominios ya scaffoldeados,
apoyandose en la justificacion que su seccion 3 da para la rama (b): el hibrido HTTP + daemon es
"apto porque todo dominio del marco corre handlers de Wolverine". Lo que ninguna de las dos
secciones registra -- la decompilacion de su "Contexto" describe *en que* delega
`ProxyTenantContext`, no *cuando* lo decide -- es que esa decision de rama ocurre en tiempo de
**construccion** del objeto (el constructor), no en tiempo de **invocacion** (cuando `HttpContext`
ya existiria). El worker aislado de Azure Functions construye el grafo de DI antes de que el
pipeline de la funcion puebla ese contexto, asi que la premisa de aptitud resulta cierta pero
insuficiente: el hibrido nunca llega a evaluar el `HttpContext` de la invocacion en curso, evalua
el que existia (ninguno) al momento de construirse.

**Cosmos.ControlPlane** (implementacion de referencia de MEF-ADR-0032 del marco) ya habia
descartado `AgregarTenantContextHibrido()` por esta misma razon y resuelto el problema con una
biblioteca propia, `Cosmos.ControlPlane.TenantResolver`: identidad ambiente respaldada por
`AsyncLocal` en vez de una decision tomada en el constructor. Este ADR porta ese patron -- ya
verificado en codigo funcionando de un consumidor real del marco -- a
`Bitakora.ControlAsistencia.TenantResolver`.

Los ADRs son la unica fuente de verdad arquitectonica del proyecto (`CLAUDE.md`). Sin un ADR local
que documente esta desviacion, un agente futuro (`reviewer`, un `implementer` en un nuevo dominio)
podria leer literalmente MEF-ADR-0028 seccion 4 y "corregir" el registro de vuelta a
`AgregarTenantContextHibrido()`, reintroduciendo el incidente. El canon del marco no se ha
enmendado todavia -- la correccion se reporto como draft al harness (ver "Consecuencias").

## Decision

**Se reemplaza `AgregarTenantContextHibrido()`/`ProxyTenantContext` por una biblioteca propia,
`Bitakora.ControlAsistencia.TenantResolver`, que resuelve la identidad (tenant + usuario + membresia de organizacion) desde un
`AsyncLocal` poblado por un middleware al inicio de cada invocacion, en vez de decidirla en el
constructor del resolver.**

### 1. `TenantExecutionContext` (`ITenantContext`, singleton)

`TenantExecutionContext` implementa `Cosmos.MultiTenancy.ITenantContext` leyendo tres
`AsyncLocal<string?>` estaticos (`_tenantId`, `_userId`, `_organizationMembershipId`) en vez de campos de instancia. El estado
es ambiente a proposito: Wolverine genera el codigo de sus handlers creando su **propio**
`IServiceScope` hijo del contenedor raiz (`IServiceScopeFactory.CreateAsyncScope()`,
`JasperFx.CodeGeneration.LazyServiceLocationFrame`), distinto del scope de la invocacion de
Functions que puebla el middleware. Un holder con estado por instancia no le llegaria al handler
-- seria otra instancia, vacia. El `AsyncLocal` fluye por la cadena async (middleware ->
`InvokeInlineAsync` -> handler) sin depender del scope de DI, asi que la identidad cruza ese
limite; mismo patron que `IHttpContextAccessor`.

Como el estado es ambiente, la instancia es un lector sin estado: se registra **singleton** (una
basta, da igual que Wolverine inyecte esa u otra instancia). Los getters `TenantId`/`UserId`/`OrganizationMembershipId` fallan
ruidosamente (`InvalidOperationException`) si la identidad no se resolvio, para que un fallo del
gateway o un mensaje sin identidad no pase desapercibido. Existe ademas `TryObtener` (version sin
lanzar) para un consumidor que decide entre la identidad ambiente y un fallback propio sin usar la
excepcion como control de flujo.

Los unicos escritores son:

- `Set` (`internal`): el middleware, caso normal.
- `SetDerivedIdentity` (`public`): para triggers que no reciben identidad del gateway porque el
  llamador no puede presentar un JWT -- webhooks de un proveedor externo, timers. El `tenantId` es
  el tenant de EJECUCION (el mismo que el gateway estampa desde el claim `tenant_id` en el resto de
  las Functions); el `actor` nombra al proceso que escribe, porque no hay usuario detras. Hace
  falta porque el sender del harness lee `ITenantContext.TenantId`/`.UserId` en cada publish
  (`Cosmos.EventDriven.CritterStack.TenancyDelivery`): sin identidad ambiente no se puede publicar.
  No se usa desde Functions HTTP detras del gateway -- ahi la identidad ya la puebla el middleware.

### Identidad de tres datos (enmienda #699, Cosmos 3.x)

Cosmos.BuildingBlocks 3.0 hace **obligatorio** `ITenantContext.OrganizationMembershipId`
(`MIGRATION.md`, PR #87): `UnitOfWorkMiddleware` lo estampa en la metadata de cada evento y
`TenancyDelivery` en cada mensaje. La identidad ambiente pasa de dos a tres datos, todos
obligatorios:

| Dato | HTTP (headers confiables, APIM) | Service Bus (`ApplicationProperties`) |
|---|---|---|
| `TenantId` | `X-Tenant-Id` | `tenant-id` |
| `UserId` | `X-User-Id` | `user_id` |
| `OrganizationMembershipId` | `X-Organization-Membership-Id` | `organization_membership_id` |

El membership id sale **del token real** (decision de la sesion planner 2026-10-01, opcion A): el
gateway APIM lo emite desde un claim del JWT (#696) y los MCP lo propagan (#697). Se descarto un
valor fijo en el BC (opcion B). La ausencia de cualquiera de los tres datos lanza
`InvalidOperationException` de infraestructura -> 500, coherente con #666 (no es una violacion de
regla de negocio ni debe traducirse a 409). `SetDerivedIdentity` debe recibir tambien el membership
id del proceso que escribe.

### 2. `TenantContextMiddleware` (`IFunctionsWorkerMiddleware`)

Puebla `TenantExecutionContext` al inicio de cada invocacion, leyendo el contexto de ejecucion del
trigger en dos planos:

- **HTTP**: headers confiables `X-Tenant-Id`/`X-User-Id`/`X-Organization-Membership-Id` (via `FunctionContext.GetHttpContext()`)
  -- los mismos que la politica global de APIM estampa desde los claims `tenant_id`/`user_email`
  del JWT (MEF-ADR-0032 del marco, seccion 4/5).
- **Service Bus**: `ApplicationProperties` `tenant-id`/`user_id`/`organization_membership_id` del mensaje, obtenidas tipadas con
  `BindInputAsync` (la maquinaria de binding cachea el resultado, no re-convierte ni settlea el
  mensaje). `tenant-id` es la llave con que Wolverine serializa el `TenantId` del envelope
  (`Wolverine.EnvelopeConstants.TenantIdKey`); `user_id` viaja bajo
  `Cosmos.MultiTenancy.TenancyHeaders.UserId`.

Es el unico punto de poblacion para los triggers que reciben identidad del gateway o del mensaje.
Los que no la reciben la derivan de su payload ya verificado y la pueblan con
`TenantExecutionContext.SetDerivedIdentity` (ver punto 1).

### 3. Wiring

- `TenancyServiceCollectionExtensions.AgregarTenantContextControlAsistencia()`: registra
  `TenantExecutionContext` como `ITenantContext` (`RemoveAll` + `AddSingleton`) dentro del seam
  `Infraestructura/ComposicionServicios.cs` de cada dominio (MEF-ADR-0029 del marco).
- `TenancyBuilderExtensions.UsarTenantContextMiddleware()`: registra `TenantContextMiddleware` en
  el `IFunctionsWorkerApplicationBuilder` de cada `Program.cs`.

Ambas mitades son necesarias: sin el middleware, el resolver resuelve pero nunca se puebla; sin el
registro DI, los routers/senders de Wolverine (que dependen de `ITenantContext` en su constructor)
no se pueden construir.

### 4. Que se retira

El `PackageReference` a `Cosmos.MultiTenancy.CritterStack` se elimina de los cuatro `.csproj` de
dominio (ya sin uso tras retirar `AgregarTenantContextHibrido()`/`ProxyTenantContext`).

### 5. Orden de despliegue de la actualizacion a Cosmos 3.x

Los mensajes en vuelo sin `organization_membership_id` caerian a dead letter en los Function Apps
3.x. Orden obligatorio:

1. Gateway APIM (#696) y MCP (#697) primero: deben emitir/propagar `X-Organization-Membership-Id`.
2. Fixtures de smoke antes de los Function Apps: deben enviar el header.
3. Vaciar las colas/subscriptions de Service Bus antes de desplegar los Function Apps en 3.x.
4. Desplegar los Function Apps en 3.x (#700).

## Alternativas consideradas

**Mantener `AgregarTenantContextHibrido()` y envolver el `HttpContext` en un accessor que se
resuelva de forma perezosa.** Descartada: el problema no es que `IHttpContextAccessor` este mal
configurado, es que `ProxyTenantContext` decide la rama en el constructor -- ningun wrapper
alrededor del accessor cambia *cuando* se evalua esa decision. Habria que parchear o reimplementar
el propio `ProxyTenantContext`, que es codigo de un paquete privado del marco sin superficie de
extension.

**Volver a `TenantResolverMonoTenantPorDefecto` (etapa a, CA-ADR-0027) y no adoptar WorkOS+APIM.**
Descartada: WorkOS+APIM (MEF-ADR-0032 del marco) ya esta desplegado y en uso en dev (#545) y
resuelve un problema real (identidad de usuario/tenant real en vez de valores fijos); revertir la
etapa perderia esa capacidad, no solo el bug.

## Consecuencias

- **Desviacion explicita de MEF-ADR-0028 seccion 4 (canon del marco)**: este proyecto no auto-cablea
  `AgregarTenantContextHibrido()` al migrar (a)->(b), usa esta biblioteca propia en su lugar. Un
  agente que lea MEF-ADR-0028 seccion 4 aisladamente veria el patron "correcto" segun el marco;
  este ADR es el que documenta por que este proyecto se aparta de el, y `reviewer`/`implementer`
  deben tratar esta desviacion como intencional, no como deuda a corregir.
- **Si el marco enmienda MEF-ADR-0028** (por ejemplo, adoptando el patron AsyncLocal como nueva
  forma canonica de la etapa (b), o corrigiendo `Cosmos.MultiTenancy.CritterStack` para que
  `ProxyTenantContext` decida por invocacion en vez de por construccion) -- este ADR es el punto de
  revision: hay que releer la enmienda y decidir si `Bitakora.ControlAsistencia.TenantResolver`
  puede retirarse en favor del paquete del marco, o si se mantiene por alguna diferencia de
  comportamiento no cubierta por la enmienda (por ejemplo, `SetDerivedIdentity` para triggers sin
  JWT, que `ProxyTenantContext` no contempla).
- **Un dominio nuevo scaffoldeado por `domain-scaffolder`** nacera con el resolver roto hasta que el
  marco enmiende su canon. El scaffolder rama por el token `tenancy.strategy` de
  `.claude/harness.config.json` (MEF-ADR-0028 seccion 3), que `/install-apim` ya dejo en
  `"multi-tenant-header"` en este proyecto -- la rama etapa (b), que genera
  `AgregarTenantContextHibrido()` en el seam de composicion que produce. Quien
  scaffoldee un dominio nuevo aqui debe migrarlo a `AgregarTenantContextControlAsistencia()` +
  `UsarTenantContextMiddleware()`, siguiendo este ADR en vez del scaffold generado. El gate de
  composicion DI (MEF-ADR-0029) no lo detecta: `ProxyTenantContext` **se construye** sin error, y
  es justamente al invocarlo cuando falla.
- **Tests**: la biblioteca nueva trae su propia suite (portada de
  `Cosmos.ControlPlane.TenantResolver`), que cubre el cruce del scope async (identidad puesta en el
  middleware, leida dentro de un handler en un `IServiceScope` hijo) y el fallo ruidoso sin
  identidad poblada.
- **Reporte al marco**: la brecha de MEF-ADR-0028 seccion 4 (el auto-cableo asume que decidir la
  rama en el constructor es equivalente a decidirla por invocacion) se reporto como draft al
  harness -- el marco decide si enmienda su canon o si documenta el patron AsyncLocal como
  alternativa verificada para workers aislados.

## Referencias

- MEF-ADR-0028 (estrategia de tenancy del marco, canon del que este ADR se desvia): seccion 4,
  transicion automatizada (a)->(b) via WorkOS+APIM, que auto-cablea
  `AgregarTenantContextHibrido()`.
- MEF-ADR-0032 del marco (identidad y autenticacion en el borde WorkOS+APIM): fuente de los headers
  canonicos `X-Tenant-Id`/`X-User-Id` que puebla `TenantContextMiddleware` en el plano HTTP.
- CA-ADR-0027 (tenancy conjoined con tenant unico): historia previa de la tenancy local de este
  proyecto -- este ADR documenta el resolver de la etapa (b) que sucede a
  `TenantResolverMonoTenantPorDefecto` de la etapa (a).
- CA-ADR-0030 (fallos sincronos en comandos HTTP sin consumidores): el catch que traducia la
  `InvalidOperationException` de `ProxyTenantContext` en un 409 enganoso es el mismo mecanismo que
  este ADR fijo para rechazos legitimos de negocio -- el sintoma se confundio con una violacion de
  regla de negocio real hasta la investigacion del hotfix.
- Fuente de verdad del patron: `Cosmos.ControlPlane.TenantResolver` (repo `Cosmos.ControlPlane`,
  implementacion de referencia), portado con la misma estructura
  (`TenantExecutionContext`/`TenantContextMiddleware`/extensiones de builder y de
  `IServiceCollection`) a `src/Bitakora.ControlAsistencia.TenantResolver/`.
- Commit 66e7a80 (hotfix, 2026-09-01): diagnostico completo del root cause y la migracion de los
  cuatro dominios.

## Control de cambios

- 2026-09-01: creacion (issue #552), documentando el hotfix 66e7a80 del mismo dia.
- 2026-10-01 (issue #699): enmienda previa a la actualizacion a Cosmos.BuildingBlocks 3.1.0 (CVE-2026-75513 de Marten 9.12.0). Identidad de tres datos (`OrganizationMembershipId` obligatorio, fuentes HTTP y Service Bus), nombres de Cosmos 3.x, orden de despliegue. Observacion: MEF-ADR-0028 puede seguir citando `ITenantResolver`; lo resuelve el marco al adoptar Cosmos 3.x.
