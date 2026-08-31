# CA-ADR-0027: Tenancy conjoined operando con un unico tenant

## Estado

Aceptado

## Contexto

El upgrade de los building blocks `Cosmos.Event*` de 0.1.9 a 2.1.0 (issue #207, commit affa2e0) es
un breaking change no detectado: en 2.x los metodos de extension `AgregarWolverineCommandRouter()`,
`AgregarWolverinePrivateEventRouter()` y `AgregarWolverineQueryRouter()` dejaron de auto-registrar un
`Cosmos.MultiTenancy.ITenantResolver` por defecto -- ese registro se movio a un paquete aparte,
`Cosmos.MultiTenancy.CritterStack` -- pero el constructor de los routers/senders
(`WolverineCommandRouter`, `WolverineQueryRouter`, `WolverinePrivateEventRouter`,
`WolverinePublicEventSender`, `WolverinePrivateEventSender`) lo sigue exigiendo por DI. Como ningun
`Program.cs` de este producto registraba un `ITenantResolver`, toda activacion de funcion en
ControlHoras y Programacion fallaba en el Function App desplegado con
`InvalidOperationException: Unable to resolve service for type 'Cosmos.MultiTenancy.ITenantResolver'`
(~461 excepciones en 24h en App Insights; investigacion completa en la field note de bug
correspondiente al issue #219).

Compilaba en Release y la suite de tests unitarios seguia verde porque la firma publica de los
metodos de extension no cambio y los tests unitarios no construyen el grafo DI completo del host de
Functions -- el fallo solo aparece en runtime al resolver el contenedor.

`Cosmos.MultiTenancy` 2.1.0 expone dos resolvers listos para usar, ademas de la interfaz:

```csharp
public interface ITenantResolver { string TenantId { get; } string UserId { get; } }
```

- `AgregarTenantResolverHibrido()` / `ProxyTenantResolver`: resuelven `TenantId` y `UserId` a partir
  de headers HTTP (`TenantId`, `user_id`) y lanzan si faltan.

Ninguno encaja con este producto: **la infraestructura de Bitakora.ControlAsistencia es multi-tenant
conjoined (`Events.TenancyStyle = Conjoined`, `Policies.AllDocumentsAreMultiTenanted()`,
`AgregarConfiguracionMartenComandos`), pero opera con un unico tenant logico** -- el default de
Marten, resuelto siempre por `TenantResolverMonoTenantPorDefecto` (ver Decision). Los clientes HTTP (Postman, smoke
tests, front futuro) no envian headers de tenant, y exigirlos romperia todos los requests existentes
sin aportar nada -- no hay hoy mas de un tenant que resolver, aunque el modelo de datos ya sea
conjoined.

## Decision

**Se conserva la infraestructura multi-tenant conjoined que fija `AgregarConfiguracionMartenComandos`
y se opera sobre ella con un unico tenant logico: se implementa un `ITenantResolver` propio, de
valores fijos, en vez de adoptar los resolvers header-based de 2.x.**

1. Clase `TenantResolverMonoTenantPorDefecto : ITenantResolver` en `Infraestructura/` de cada
   dominio (`Colaboradores`, `ControlHoras`, `Programacion` y `Sedes`). Se acepta duplicar la clase
   en lugar de extraerla a un proyecto compartido (Rule of Three, MEF-ADR-0018); tampoco vive en
   `Contracts`, reservado a eventos publicos y value objects compartidos (CA-ADR-0002).
2. `TenantId` resuelve al tenant por defecto de Marten: `JasperFx.StorageConstants.DefaultTenantId`
   (valor `"*DEFAULT*"`). Marten mapea ese valor a `Tenancy.Default` aun con
   `options.Policies.AllDocumentsAreMultiTenanted()` activo -- no se introduce un tenant nuevo, se
   reutiliza el que Marten ya asume cuando no se le indica nada distinto.
3. `UserId` es el literal fijo `"usuario-no-autenticado"`: el producto no distingue usuarios
   todavia: no hay autenticacion de llamador que popular este campo con un valor real. Es, ademas,
   una marca explicita en la metadata de eventos de que el evento se origino sin autenticacion.
4. Ambos valores se exponen unicamente via los getters que pide la interfaz -- sin superficie
   publica adicional (encapsulamiento, MEF-ADR-0012).
5. Registro en DI con lifetime **Scoped** en ambos `Program.cs`, junto a los
   `AgregarWolverine*Router`/`AgregarWolverineEventSender` correspondientes: los routers/senders que
   lo inyectan son Scoped, y al ser un unico registro Scoped por request/activacion, el lado que
   publica (HTTP) y el lado que consume (Service Bus) devuelven los mismos valores fijos, lo que
   garantiza consistencia de particion entre ambos.

## Consecuencias

- El contenedor DI de ambos Function Apps vuelve a resolver `ICommandRouter`, `IQueryRouter` y (en
  ControlHoras) `IPrivateEventRouter` sin excepcion, sin exigir headers que este producto no envia.
- Si el proyecto evoluciona a multi-tenant real (mas de un tenant logico, aislamiento de datos por
  cliente), **este ADR es el punto de revision**: habria que reemplazar
  `TenantResolverMonoTenantPorDefecto` por un
  resolver que derive el tenant de una fuente real (header, claim de autenticacion, subdominio, etc.)
  y decidir entonces si adoptar `AgregarTenantResolverHibrido()`/`ProxyTenantResolver` de
  `Cosmos.MultiTenancy.CritterStack` en vez de la implementacion propia.
- **Todo proceso que lea el event store debe declarar la misma tenancy conjoined que el write-side
  escribio**, no solo el que escribe: el worker de proyecciones lo hace en el named store de cada
  dominio (`ConfiguracionMartenProjections{Dominio}`, issue #268), con guardas de config-test que
  fallan si esa linea desaparece. Un lector que quede con el default `Single` no rompe el build --
  falla en runtime, leyendo un event store que no encuentra.
- Este fix es puntual al registro del resolver: no se tocan los sitios de `Invoke`/publicacion de los
  comandos y eventos existentes, que ya construyen internamente `DeliveryOptions` con
  `TenantId`/`user_id` a partir del resolver inyectado.
- Un upgrade futuro de `Cosmos.Event*`/`Cosmos.MultiTenancy` que vuelva a mover responsabilidades de
  auto-registro de DI a otro paquete puede repetir este mismo sintoma (compila y pasa tests
  unitarios, falla solo en runtime desplegado). El guardrail de proceso para detectarlo antes de
  desplegar (test de composicion del `IHost`/`FunctionsApplication`, o smoke minimo obligatorio
  pre-merge) queda fuera de alcance de este ADR y se rastrea en un issue aparte.

## Control de cambios

- **2026-07-31 (issue #268)**: renombrado el archivo (de
  `ca-adr-0027-estrategia-tenancy-mono-tenant.md` a
  `ca-adr-0027-tenancy-conjoined-con-tenant-unico.md`) y corregidos titulo, contexto y decision.
  El titulo y el contexto originales afirmaban que el producto es mono-tenant, pero la propia
  decision #2 de este ADR ya describia el modelo real: Marten mapea el tenant fijo a
  `Tenancy.Default` **aun con `AllDocumentsAreMultiTenanted()` activo** -- es decir, la
  infraestructura ya es multi-tenant conjoined, y lo que es fijo es el numero de tenants logicos
  operando sobre ella (uno). Era drift de nomenclatura, no drift de codigo: se detecto al alinear
  el named store del worker de proyecciones con esta misma tenancy (issue #268), que exigio leer
  este ADR para decidir si el worker debia declarar `TenancyStyle.Conjoined`. Las consecuencias no
  cambiaron de fondo; se sumo una que la version anterior no enunciaba: la obligacion se extiende a
  **todo** proceso lector del event store, no solo al que escribe.

- **2026-08-31 (issue #536)**: alineada la forma local a la canonica del marco (MEF-ADR-0028
  seccion 2) en los cuatro dominios: la clase `TenantResolverFijo` pasa a
  `TenantResolverMonoTenantPorDefecto`, el registro DI a
  `services.AddScoped<ITenantResolver, TenantResolverMonoTenantPorDefecto>();` y el literal de
  `UserId` de `"sin-identificar"` a `"usuario-no-autenticado"`. **Motivo**: la migracion
  automatizada de etapa (a) -> (b) que ejecuta `/install-apim` (MEF-ADR-0028 seccion 4, paso 9.3
  del skill) detecta los dominios migrables por esa linea de registro **exacta**; cualquier otra
  forma se reporta como "pendiente de revision manual" y no se toca. Sin esta alineacion, la
  adopcion de WorkOS AuthKit + APIM (MEF-ADR-0032) dejaria los cuatro dominios sin migrar.
  **La decision de fondo no cambia**: la infraestructura sigue siendo conjoined y sigue operando
  con un unico tenant logico -- el proyecto permanece en etapa (a) hasta que se corra
  `/install-auth`. Efecto observable unico: el literal de `UserId` en la metadata de eventos
  nuevos; aceptado explicitamente por el product owner (los datos de dev son desechables). La
  decision #4 se reformulo porque la forma canonica expresa los dos valores directamente en los
  getters, sin constantes `private` intermedias: el invariante que ese punto protege -- cero
  superficie publica mas alla de la interfaz (MEF-ADR-0012) -- se conserva igual.
