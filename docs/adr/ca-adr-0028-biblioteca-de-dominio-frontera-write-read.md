# CA-ADR-0028: Biblioteca de dominio por dominio como frontera entre write-side y read-side

## Estado

**Superado por CA-ADR-0029** (issue #237, 2026-07-31). La biblioteca
`Bitakora.ControlAsistencia.{Dominio}.Dominio` que este ADR propone nunca se creó.

Qué se conserva y qué no:

- **Se conserva el diagnóstico**, que era correcto: el worker de proyecciones no puede ver los tipos
  de los eventos que proyecta, y no puede alcanzarlos referenciando un Function App.
- **Se conserva la prohibición de la decisión #4** --el worker nunca referencia el `.csproj` de un
  Function App-- y las dos alternativas descartadas de este ADR siguen siendo válidas tal como están
  argumentadas.
- **Queda superada la decisión #1**: mandaba mover también los aggregates y los value objects del
  dominio. Al verificar objeto por objeto qué necesita el worker, esa ampliación resultó innecesaria
  (una `SingleStreamProjection` se declara sobre tipos de evento, no sobre el aggregate) y habría
  obligado al worker a cargar la lógica de cálculo de horas que nunca usa. CA-ADR-0029 mueve
  únicamente los eventos y su payload.
- **Corrección de un error factual**: el Contexto de este ADR afirma que "ninguno de los cinco
  implementa `IPublicEvent`/`IPrivateEvent`". Es falso: `MarcacionRegistrada` implementa
  `IPrivateEvent` --se publica al topic `marcacion-registrada` (issue #213)-- y además se persiste en
  el stream de `RegistroDeMarcacionAggregateRoot`. Ese doble rol es justamente el caso incómodo que
  CA-ADR-0029 documenta como deuda asumida y que el issue #270 resuelve.

El texto original se conserva abajo como registro histórico.

---

Aceptado (histórico)

## Contexto

Al planear la infraestructura de proyecciones (MEF-ADR-0034 del harness, "Worker de proyecciones y
read models por Bounded Context") se descubrió un obstáculo estructural que ningún ADR -- ni local
ni del marco -- resuelve: **el worker de proyecciones no puede ver los tipos de los eventos que
tendría que proyectar.**

Hoy los eventos del aggregate viven dentro del proyecto Function App de cada dominio, en la carpeta
vertical de la feature que los emite (verificado por inspección directa del código):

- `src/Bitakora.ControlAsistencia.Programacion/CrearTurnoFunction/Eventos/TurnoCreado.cs`
- `src/Bitakora.ControlAsistencia.Programacion/SolicitarProgramacionTurnoFunction/Eventos/ProgramacionTurnoSolicitada.cs`
- `src/Bitakora.ControlAsistencia.ControlHoras/RegistrarMarcacionFunction/Eventos/MarcacionRegistrada.cs`
- `src/Bitakora.ControlAsistencia.ControlHoras/AdicionarMarcacionCuandoMarcacionRegistrada/Eventos/MarcacionAdicionada.cs`
- `src/Bitakora.ControlAsistencia.ControlHoras/AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction/Eventos/TurnoDiarioAsignado.cs`

Los aggregates que los aplican (`CatalogoTurnos`, `SolicitudProgramacionAggregateRoot`,
`RegistroDeMarcacionAggregateRoot`, `ControlDiarioAggregateRoot`) viven igual de adentro,
en `Entities/` de cada Function App. Todos estos eventos son **internos al event sourcing del
aggregate**: `ProgramacionTurnoSolicitada` lo documenta explícitamente en su propio código ("Evento
de event sourcing (privado)... No se publica al Service Bus"), y ninguno de los cinco implementa
`IPublicEvent`/`IPrivateEvent` -- esa es la marca que distingue a los DTOs de `Contracts`
(`DiaCalculado`, `ProgramacionTurnoDiarioSolicitada`), que sí cruzan el bus. Varios de estos eventos
tienen constructor privado y exponen `ConfigurarSerializacion(resolver)` (MEF-ADR-0012), atados al
resolver custom de Marten que hoy registra
`src/Bitakora.ControlAsistencia.ControlHoras/Infraestructura/ConfiguracionSerializacionControlHoras.cs`
-- ese archivo también vive dentro del Function App.

MEF-ADR-0034 sección 5 (del marco) fija que las clases de proyección viven en el worker
(`<RootNamespace>.Projections`) y que `<RootNamespace>.ReadModels` no lleva Marten ni
transitivamente. Una clase de proyección declara `Create(TurnoCreado e)` / `Apply(MarcacionAdicionada
e, view)`: necesita los tipos `TurnoCreado`/`MarcacionAdicionada` en tiempo de compilación. El ADR del
marco asume implícitamente que el worker los alcanza, pero no dice cómo -- y las dos rutas obvias para
alcanzarlos (que el worker referencie el `.csproj` del Function App, o que los eventos se muden a
`Contracts`) tienen problemas que se documentan en "Alternativas consideradas".

La decisión de fondo ya se tomó en una sesión de planning (2026-07-27): se extrae una biblioteca de
dominio por dominio. Este ADR la fija por escrito, deliberadamente **antes** de implementarla: si no
existe, el agente `projections-scaffolder` o `projection-implementer` del marco tomarán la decisión
implícitamente al toparse con el problema, y lo más probable es que tomen el atajo de referenciar el
Function App directamente. La extracción concreta (mover los archivos, ajustar namespaces, migrar los
`.resx` asociados) es un issue hermano, bloqueado por este.

## Decisión

### 1. Se extrae `Bitakora.ControlAsistencia.{Dominio}.Dominio` por cada dominio con proyecciones

Por cada dominio del bounded context que registre proyecciones (hoy: `Programacion`, `ControlHoras`),
se extrae una biblioteca de clases `Bitakora.ControlAsistencia.{Dominio}.Dominio` que aloja:

- Los **aggregates** (`Entities/*AggregateRoot.cs`, `Entities/CatalogoTurnos.cs`).
- Los **eventos internos del aggregate** -- los que hoy viven en `Eventos/` dentro de cada Function
  App y que ni son `IPublicEvent` ni `IPrivateEvent` (esos siguen en `Contracts`, ver decisión #2).
- Los **value objects del dominio** que esos eventos y aggregates usan y que no son vocabulario
  compartido entre dominios (los que sí lo son quedan en `Contracts`, CA-ADR-0002).

Es el modelo de dominio rico del write-side, con su identidad string (`ControlDiarioAggregateRoot`
usa `"{EmpleadoId}:{Fecha:yyyy-MM-dd}"`, `CatalogoTurnos` usa `evento.TurnoId.ToString()`) y sus tipos
de constructor privado + `ConfigurarSerializacion` (MEF-ADR-0012) intactos.

### 2. Tres bibliotecas, tres propósitos, sin solape

| Biblioteca | Contenido | Depende de Marten | Cruza el bus |
|---|---|---|---|
| `Contracts` | DTOs planos y value objects **compartidos entre dominios** (CA-ADR-0002) | No (verificado: su único `PackageReference`, `Cosmos.EventDriven.Abstractions` 2.1.0, no arrastra Marten ni transitivamente -- comprobado contra `project.assets.json`, no por inspección visual) | Sí -- es exactamente lo que puede viajar en `IPublicEvent`/`IPrivateEvent` |
| `{Dominio}.Dominio` (este ADR) | Aggregates, eventos internos del aggregate, value objects propios del dominio | No como paquete propio, pero sus tipos exigen el resolver custom de Marten para (de)serializarse (MEF-ADR-0012) | No -- son el modelo rico que CA-ADR-0025 prohíbe exponer al bus |
| `ReadModels` (MEF-ADR-0034 sección 5) | Records de vista que las proyecciones producen | No, ni transitivamente (mandato explícito de MEF-ADR-0034 sección 5) | No -- solo los consume el propio read-side |

No hay solape: `Contracts` es vocabulario cross-domain que cruza el bus; `{Dominio}.Dominio` es el
modelo rico que un solo dominio posee y que CA-ADR-0025 ya prohíbe publicar; `ReadModels` es la
vista derivada, sin comportamiento ni Marten. Cada una resuelve una necesidad distinta y ninguna
sustituye a otra.

### 3. Sentido de las dependencias permitidas

```
Contracts  <---------------------  {Dominio}.Dominio  <---------------------  Function App (write-side)
    ^                                       ^
    |  (opcional, verificado sin Marten)    |  (para tipar Create/Apply)
    |                                       |
ReadModels  <---------------------  Projections worker (<RootNamespace>.Projections)
```

- **Function App (write-side) de un dominio** referencia `{Dominio}.Dominio` (el suyo, nunca el de
  otro dominio -- los dominios siguen siendo autónomos, CA-ADR-0001) y `Contracts` (para el
  vocabulario compartido y para publicar/consumir eventos públicos/privados). Esto no cambia frente a
  hoy; solo se explicita que el modelo rico que antes vivía inline en el Function App ahora vive en un
  `.csproj` separado que el Function App referencia.
- **El worker de proyecciones** (`<RootNamespace>.Projections`, MEF-ADR-0034 sección 1) referencia
  `{Dominio}.Dominio` de **cada** dominio cuyos eventos proyecta (para tipar `Create`/`Apply` sobre los
  tipos de evento reales) y `ReadModels` (para el tipo de vista que produce). Nunca referencia el
  `.csproj` de un Function App (decisión #4).
- **`ReadModels`** puede referenciar `Contracts` cuando una vista necesite un value object compartido
  (p. ej. `EmpleadoId`), porque se verificó que no arrastra Marten. **No** referencia
  `{Dominio}.Dominio`: las clases de proyección (en el worker) son las que traducen de un tipo al
  otro; `ReadModels` solo aloja el resultado.
- **`{Dominio}.Dominio`** puede referenciar `Contracts` cuando un aggregate o evento interno use un
  value object compartido (patrón que ya existe hoy: `CatalogoTurnos` referencia
  `Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects`). Nunca referencia `ReadModels` ni
  el `.csproj` de ningún Function App.

### 4. Prohibición explícita: el worker nunca referencia el `.csproj` de un Function App

El worker de proyecciones **no puede**, bajo ninguna circunstancia, agregar un `ProjectReference` al
`.csproj` de un Function App (ni el propio ni el de otro dominio) para alcanzar sus tipos de evento.
La única vía para que el worker vea un tipo de evento del aggregate es a través de
`{Dominio}.Dominio`. Ver "Alternativas consideradas" para el razonamiento completo.

## Alternativas consideradas

### Alt 1: el worker referencia directamente el `.csproj` del Function App

**Descartada.** El Function App de un dominio arrastra el Azure Functions Worker SDK
(`Microsoft.Azure.Functions.Worker.*`), Wolverine (mensajería, `IPrivateEventSender`/
`IPublicEventSender`) y el hosting HTTP de ASP.NET Core -- ninguno de los tres tiene sentido en un
proceso que, según MEF-ADR-0034 sección 4, "no toca Azure Service Bus" y cuya única dependencia externa
es la connection string de PostgreSQL. Además, el `Program.cs`/`Infraestructura/` de un Function App es
un composition root pensado para ejecutarse como Function App (MEF-ADR-0034 sección 9 los excluye del
coverage gate justamente por ser wiring puro, no superficie reusable): referenciarlo desde el worker
acoplaría el ciclo de vida de compilación/despliegue de ambos procesos sin ninguna necesidad real,
inflaría la imagen del Container App (MEF-ADR-0034 sección 8) con paquetes que nunca se usan en
runtime, y arriesgaría inicializar accidentalmente wiring de Service Bus o de Functions dentro del
worker.

### Alt 2: mover los eventos del aggregate a `Contracts`

**Descartada.** Violaría dos ADRs locales ya aceptados:

- **CA-ADR-0002**: `Contracts` es vocabulario **compartido entre dominios** que cruza el bus; los
  eventos del aggregate no cruzan el bus (`ProgramacionTurnoSolicitada` lo documenta explícitamente:
  "No se publica al Service Bus") y no son compartidos -- cada uno pertenece a un solo dominio.
- **CA-ADR-0025**: fija que "el modelo de dominio rico vive en su dominio y nunca cruza el bus"
  precisamente porque moverlo a `Contracts` reintrodujo, en el pasado (`DiaCalculado`, corregido en
  `#183`/`#184`), un bug de serialización lossy: el canal de publicación a Service Bus usa el
  serializador **por defecto** (sin el resolver custom de MEF-ADR-0012), así que un tipo con
  constructor privado como `TurnoCreado` -- que hoy expone `ConfigurarSerializacion` precisamente
  porque necesita ese resolver -- se serializaría de forma corrupta hacia cualquier consumidor externo
  si viviera en `Contracts`. Repetir ese movimiento con los eventos del aggregate reproduciría el mismo
  bug que CA-ADR-0025 ya pagó el costo de corregir.

## Consecuencias

### Positivas

- El worker de proyecciones tiene una vía de acceso a los tipos de evento sin arrastrar dependencias
  ajenas a su propósito (Azure Functions SDK, Wolverine, ASP.NET Core).
- Se preserva la frontera que CA-ADR-0002/CA-ADR-0025 ya fijaron: `Contracts` sigue siendo
  exclusivamente vocabulario plano cross-domain: nada de esta decisión le agrega superficie.
- El modelo de dominio rico queda en un único lugar con dos consumidores legítimos (Function App propio
  y worker de proyecciones), en vez de duplicarse o de forzar un acoplamiento incorrecto.
- La regla "el worker nunca referencia un Function App" es verificable mecánicamente (revisar
  `ProjectReference` en el `.csproj` del worker) y queda disponible para que `reviewer` la aplique.

### Negativas

- **Costo de refactor no trivial**, explícitamente diferido al issue hermano de extracción, no a este:
  dos dominios (`Programacion`, `ControlHoras`) deben mover sus aggregates, sus eventos internos, sus
  value objects de dominio y los archivos `.resx` asociados (`RetardoMensajes.resx`,
  `IntervaloTemporalMensajes.resx`, `IntervaloClasificadoMensajes.resx`, `MomentoDelDiaMensajes.resx`,
  `TurnoCreadoMensajes.resx`, entre otros) a los nuevos proyectos `{Dominio}.Dominio`, actualizar todos
  los `using` y namespaces que los referencian, y verificar que cada Function App siga compilando y
  pasando sus tests tras el movimiento. Ese trabajo se ejecuta en la tanda de proyecciones (cuando el
  `projections-scaffolder`/`projection-implementer` lo necesiten), no en este issue.
- **Un `.csproj` más por dominio** que mantener y versionar; el equipo debe recordar que un aggregate o
  evento interno nuevo se crea directamente en `{Dominio}.Dominio`, no en el Function App como se hacía
  hasta ahora.
- Este ADR no resuelve dónde vive `ConfigurarSerializacion`/el resolver custom de Marten
  (`ConfiguracionSerializacionControlHoras` y equivalentes): si viaja con `{Dominio}.Dominio` o si el
  worker necesita su propia réplica para poder deserializar eventos con constructor privado al leerlos
  de Postgres. Queda para el issue hermano en borrador que ya se identificó para ese problema
  específico.

### Fuera de alcance / trabajo diferido

- La extracción concreta de archivos (issue hermano, bloqueado por este).
- La réplica o reubicación de la configuración de serialización custom de Marten (issue hermano en
  borrador).
- Cualquier cambio al contrato del token `projections.enabled` de `harness.config.json` o a los
  agentes del marco (`projections-scaffolder`, `projection-implementer`): ese código pertenece al
  plugin Mefisto, no a este repositorio.

## Referencias

- CA-ADR-0001 ("Function App por dominio"): fija la autonomía de dominio que esta decisión respeta
  (`{Dominio}.Dominio` nunca referencia el de otro dominio).
- CA-ADR-0002 ("Proyecto Contracts para eventos y value objects compartidos"): delimita la frontera
  de `Contracts` frente a `{Dominio}.Dominio` (decisión #2).
- MEF-ADR-0012 (encapsulamiento, `ConfigurarSerializacion`, constructor privado): los tipos que se
  mudan a `{Dominio}.Dominio` siguen este patrón sin cambios.
- CA-ADR-0025 ("El modelo de dominio rico no cruza el bus"): razón directa por la que la Alt 2 se
  descartó; premisa que este ADR cita y refuerza.
- MEF-ADR-0034 del harness ("Worker de proyecciones y read models por Bounded Context"), sección 5:
  fija que `ReadModels` no lleva Marten ni transitivamente y que las clases de proyección viven en el
  worker -- las dos restricciones que, combinadas, crean el problema que este ADR resuelve.
- Verificación propia: `src/Bitakora.ControlAsistencia.Contracts/obj/project.assets.json` (tras
  `dotnet restore`), sin ninguna entrada de Marten para el `PackageReference` único de `Contracts`
  (`Cosmos.EventDriven.Abstractions` 2.1.0).

## Control de cambios

- 2026-07-27: creación. Fija la extracción de `Bitakora.ControlAsistencia.{Dominio}.Dominio` como
  ubicación canónica del modelo de dominio rico (aggregates, eventos internos del aggregate, value
  objects de dominio), delimita su frontera contra `Contracts` y `ReadModels`, fija el sentido de las
  dependencias permitidas y prohíbe explícitamente que el worker de proyecciones referencie el
  `.csproj` de un Function App. Numerado 0028 por ser el siguiente libre de la serie local (0001-0027
  ya ocupados). No ejecuta ningún refactor: la extracción concreta queda en el issue hermano que este
  ADR desbloquea.
