# CA-ADR-0029: Ensamblados de eventos particionados por rol del evento

## Estado

Aceptado. Supera a CA-ADR-0002 y a CA-ADR-0028.

## Contexto

El worker de proyecciones (`Bitakora.ControlAsistencia.Projections`) no podía ver los tipos de los
eventos que tendría que proyectar: vivían dentro del proyecto Function App de cada dominio, y el
worker no puede referenciarlo porque arrastraría Azure Functions SDK, Wolverine y ASP.NET Core a un
proceso que solo lee Postgres (MEF-ADR-0034 sección 5).

CA-ADR-0028 ya había identificado el problema y propuesto extraer una biblioteca
`{Dominio}.Dominio` con los aggregates, los eventos internos y los value objects del dominio. Al
verificar objeto por objeto qué necesitaba el worker, esa solución resultó más ancha de lo necesario
y su criterio de inclusión, difuso. El worker necesita exactamente dos cosas:

1. **Ver los 5 eventos persistidos en compilación**, para declarar `Create`/`Apply` sobre ellos. Son
   los que aplican los 4 aggregates: `TurnoCreado`, `ProgramacionTurnoSolicitada`,
   `MarcacionRegistrada`, `MarcacionAdicionada`, `TurnoDiarioAsignado`. `DiaCalculado` **no** entra:
   ningún aggregate lo aplica, solo se publica al bus.
2. **Poder deserializarlos**: invocar el `ConfigurarSerializacion` de los tipos ricos involucrados.

No necesita los aggregates (una `SingleStreamProjection` se declara sobre tipos de evento, no sobre
el aggregate) ni los value objects de cálculo.

En paralelo, `Contracts` había acumulado tres familias distintas de tipos porque su criterio de
inclusión --"lo compartido"-- no excluía nada: eventos que salen del bounded context, eventos que
circulan por el bus interno, y modelo rico que se persiste en el event store.

## Decisión

### 1. La partición es por rol del evento, no por "modelo de dominio"

El criterio "se persiste" frente a "se publica" es el único que un compilador puede hacer cumplir, y
por eso reemplaza a "lo compartido":

| Ensamblado | Criterio de inclusión | Forma de los tipos |
|---|---|---|
| `PublicEvents` | Se publica a un bus y **sale** del bounded context | Plano y portable (serializador por defecto) |
| `PrivateEvents` | Se publica a un bus **interno** del BC | Plano y portable (serializador por defecto) |
| `{Dominio}.DomainEvents` | Se **persiste** en el event store de ese dominio | Puede ser rico (ctor privado + resolver custom) |

Un ensamblado de eventos contiene los eventos y los tipos que los componen (su payload). No contiene
aggregates, ni value objects de cálculo, ni comandos.

### 2. Tres islas: cero referencias entre los ensamblados de eventos

*Enmienda incorporada por el issue #317, que alinea esta decisión con el canon del marco
(MEF-ADR-0039 decisión #2, ver decisión #7 de este ADR). Reescribe **in situ** la versión original
de esta decisión -- el grafo encadenado de abajo --, que el marco descartó como Alt 5 de
MEF-ADR-0039.*

Los tres ensamblados de eventos de la decisión #1 -- `PublicEvents`, `PrivateEvents`,
`{Dominio}.DomainEvents` -- son **tres islas**: cada uno declara **cero `<ProjectReference>`**, ni
hacia los otros dos ni hacia ningún otro proyecto del repo. Los `PackageReference` no quedan
restringidos por esta decisión (los dos ensamblados de bus llevan el paquete de markers
`IPublicEvent`/`IPrivateEvent`). La composición ocurre exclusivamente en los dos puntos que la
necesitan -- el Function App de cada dominio y el worker de proyecciones:

```
PublicEvents        PrivateEvents        Programacion.DomainEvents      ControlHoras.DomainEvents
(cero refs)          (cero refs)              (cero refs)                     (cero refs)

Referenciados directamente, sin transitividad, por:

Function App Programacion   ->  Programacion.DomainEvents + PrivateEvents + PublicEvents
Function App ControlHoras   ->  ControlHoras.DomainEvents + PrivateEvents + PublicEvents

Projections (worker)        ->  Programacion.DomainEvents + ControlHoras.DomainEvents + ReadModels
                                 (sin referencia directa a PublicEvents/PrivateEvents, nunca un
                                  Function App; ver nota de transitividad via ReadModels abajo)
```

- El **Function App** de cada dominio referencia los tres ensamblados de eventos directamente: su
  propio `{Dominio}.DomainEvents`, más `PrivateEvents` y `PublicEvents` del BC.
- El **worker de proyecciones** referencia `{Dominio}.DomainEvents` de cada dominio que proyecta más
  `ReadModels` -- ningún `<ProjectReference>` **directo** hacia `PublicEvents`/`PrivateEvents` ni
  hacia el `.csproj` de un Function App (MEF-ADR-0034 sección 5, MEF-ADR-0039 decisión #4).

El propósito declarado de ese grafo en el canon es que el worker no arrastre `PublicEvents`/
`PrivateEvents` **ni transitivamente**. En este repo eso todavía no se cumple, por una vía que no
es ninguno de los tres ensamblados de eventos: `ReadModels` referencia ambos buses desde el issue
#289 (`TurnoDiarioView` reusa `InformacionEmpleado` de `PublicEvents` y `DetalleTurno` de
`PrivateEvents` en vez de redeclararlos), así que el worker los ve por transitividad a través de
`ReadModels`. La regla de cero `<ProjectReference>` de esta decisión **no** alcanza a `ReadModels`
--no es un ensamblado de eventos--, y por eso esa referencia no es una violación de las tres islas;
pero sí deja el propósito a medias, y queda registrada como deuda propia en "Negativas y deuda
asumida": no la paga ninguno de los issues #318/#319/#320.

**Motivación del cambio -- por qué ya no hay cadena**: cada ensamblado evoluciona a una velocidad
distinta. El bus público evoluciona bajo presión de consumidores externos y el versionado V2
(MEF-ADR-0005); el bus interno evoluciona libre dentro del BC; el event store es el contrato más
longevo de los tres -- el JSON persistido en `mt_events` se relee durante toda la vida del sistema
(MEF-ADR-0036). Con el grafo encadenado (versión original de esta decisión), un cambio hacia afuera
(evolucionar el contrato público) exigía revisar hacia adentro (el tipo persistido que lo
referenciaba), y un evento persistido cuyo payload fuera un tipo de bus quedaba amarrado a la
evolución de ese bus -- el modo de fallo concreto de ese acoplamiento se documenta en la decisión #5.

**Estado real de este repo frente a esta decisión**: no se cumple todavía -- ver "Negativas y deuda
asumida". El grafo encadenado original sigue construido en el código (`PrivateEvents` referencia
`PublicEvents`; ambos `DomainEvents` referencian los dos buses). Migrar a cero referencias es el
alcance de los issues #318 y #319; el enforcement mecánico de que no regrese, el issue #320.

### 3. Tres reglas que garantiza el grafo de compilación, no la disciplina

- Un tercero que instale el paquete de `PublicEvents` **no puede** ver eventos internos del BC: la
  unidad de distribución de NuGet es el ensamblado completo.
- ControlHoras **no puede** compilar contra `TurnoCreado`, evento interno de Programación
  (CA-ADR-0001, autonomía de dominio). Un `DomainEvents` compartido no daría esta garantía.
- Nada en `PublicEvents` puede depender de un tipo interno, porque el grafo no lo permite.

Se agrega una cuarta, del lado de los tests, generalizada por el issue #317 a los dos ensamblados de
bus (MEF-ADR-0039 decisión #7): cada uno tiene su propio proyecto de tests, que referencia
**únicamente** su propio ensamblado. `PublicEvents.Tests` referencia únicamente `PublicEvents`, de
modo que si un test suyo llegara a necesitar `PrivateEvents` o un `DomainEvents`, eso delata que el
tipo bajo prueba no es distribuible como Published Language (MEF-ADR-0005): un consumidor externo
del paquete tampoco tendría ese otro ensamblado. `PrivateEvents.Tests` referencia únicamente
`PrivateEvents`, por el mismo motivo de aislamiento aunque sin la restricción de distribución externa
(`PrivateEvents` nunca sale del BC): si un test suyo necesitara `PublicEvents`, un `DomainEvents` o
un Function App, delataría que está probando composición de dominio, no el ensamblado del bus interno
en sí mismo.

A diferencia de las tres primeras, **esta cuarta regla el grafo no la garantiza**: las tres islas de
la decisión #2 solo cierran la vía accidental --ya no existe transitividad por la que un test alcance
otro ensamblado de eventos sin declararla--, pero un `.csproj` de tests puede declarar la referencia
que quiera, porque la decisión #2 restringe los `.csproj` de los tres ensamblados de eventos, no los
de sus proyectos de test (MEF-ADR-0039 decisión #7). Por eso es un guardrail de diseño cuyo
enforcement mecánico es el issue #320, junto con el de la decisión #2 (MEF-ADR-0039 decisión #10).
Hoy ambos `.csproj` de test ya declaran esa única referencia (verificado por inspección directa:
`PublicEvents.Tests -> PublicEvents`, `PrivateEvents.Tests -> PrivateEvents`); lo que todavía no
cumple la regla es `PrivateEvents.csproj` mismo, que sigue referenciando `PublicEvents` (ver
decisión #2 y "Negativas y deuda asumida").

### 4. Un ensamblado de eventos aloja la lista completa de serialización de su dominio

`ConfiguracionSerializacionProgramacion` y `ConfiguracionSerializacionControlHoras` viven en sus
respectivos `DomainEvents` y son la única fuente de la lista. El `ComposicionServicios` de cada
Function App las invoca en vez de declarar el resolver inline, y el worker puede invocar la misma
lista en su propio store. Antes del refactor esa lista cruzaba dos proyectos y la mitad de
Programación no existía como clase, lo que hacía imposible replicarla.

### 5. Los eventos no conocen los comandos; payload por rol -- un tipo no cruza ensamblados de eventos

El factory de un evento persistido no recibe el comando que lo origina: recibe un tipo de entrada
propio del ensamblado de eventos. `TurnoCreado.Crear(Guid, string, IReadOnlyList<DatosFranja>)`, y el
comando `CrearTurno` --que se queda en la Function App con su `Franja` anidado, porque el contrato
HTTP no pertenece al ensamblado de eventos-- expone `ToDatosFranjas()` para traducirse. Un solo lugar
con ese mapeo, reusado por el handler y por sus tests.

La razón es estructural, no estética: `CrearTurno` vive en la Function App, que referencia
`Programacion.DomainEvents`, así que un factory que reciba el comando cierra un ciclo de referencias
y no compila.

*Generalización incorporada por el issue #317 (MEF-ADR-0039 decisión #6), a partir de las tres islas
de la decisión #2.* El mismo argumento aplica a cualquier payload, no solo al comando. Bajo cero
`ProjectReference` entre los tres ensamblados de eventos, **un tipo de payload no cruza ensamblados
de eventos**: cuando el mismo dato viaja por el bus (tipo de `PublicEvents` o `PrivateEvents`) y
además se persiste (tipo de `{Dominio}.DomainEvents`), cada ensamblado declara su **propio record
plano** con ese dato -- duplicación deliberada, con paridad de campos -- en vez de importar el tipo
del otro ensamblado. Todo el mapeo entre el tipo de bus y su equivalente persistido vive en el
**Function App**, el único ensamblado que ve los tres.

La motivación no es solo estructural: el bus público evoluciona bajo presión de consumidores
externos y versionado V2 (MEF-ADR-0005), el bus interno evoluciona libre dentro del BC, y el event
store es el contrato más longevo -- releído durante toda la vida del sistema (MEF-ADR-0036). Un
payload compartido por referencia entre un evento de bus y uno persistido tiene además un **modo de
fallo silencioso** verificado empíricamente en este repo (issue #270, ver "Negativas y deuda
asumida" más abajo): System.Text.Json, al deserializar un payload anidado que no calza con el tipo
esperado -- típicamente tras evolucionar un lado sin el otro --, no lanza excepción: deja los campos
no resueltos en su valor default. Un campo en su valor default dentro de un evento ya persistido en
`mt_events` es un dato corrupto que el sistema releerá durante toda su vida.

**Estado real de este repo frente a esta regla**: no se cumple todavía (ver "Negativas y deuda
asumida"). `TurnoDiarioAsignado` (persistido en `ControlHoras.DomainEvents`) embebe
`InformacionEmpleado` (tipo de `PublicEvents`) y `DetalleTurno` (tipo de `PrivateEvents`) como
payload anidado; `ProgramacionTurnoSolicitada` (persistido en `Programacion.DomainEvents`) hace lo
mismo; y `FranjaOrdinaria.ToDetalle()`/`SubFranja.ToDetalle()` retornan
`DetalleFranjaOrdinaria`/`DetalleSubFranja`, tipos de `PrivateEvents`. Pagar esta deuda es el alcance
del issue #319.

### 6. El alias es la identidad del evento persistido, y se registra explícitamente

*Enmienda incorporada por el issue #277, que paga la deuda que la primera versión de este ADR había
asumido (ver "Negativas y deuda asumida").*

La identidad con la que Marten reconoce un evento al leer un stream es su **alias** --la columna
`type` de `mt_events`--, no el nombre calificado del tipo. `EventDocumentStorage.Resolve` (Marten
9.12) busca primero un mapping por alias y solo cae a `mt_dotnet_type` -> `Type.GetType(...)` cuando
no lo encuentra; si el alias resuelve, un `mt_dotnet_type` desactualizado se ignora **por diseño**.
De ahí que mover un evento de namespace o de ensamblado no obligue a migrar ningún dato: el alias
lo deriva `EventTypeExtensions` del **nombre simple** de la clase (`eventType.Name.ToTableAlias()`),
inmune al namespace.

Lo que sí es obligatorio es que el mapping exista **antes de la primera lectura** del proceso, en vez
de depender de que un append previo lo haya poblado. Por eso cada ensamblado de eventos aloja también
la lista de sus tipos persistidos --`IdentidadEventos{Dominio}.TiposPersistidos`, hermana de la lista
de serialización de la decisión #4--, y **todo** proceso que lea esos streams la registra en su propio
`EventGraph` con `Events.AddEventTypes(...)`: el write-side en su `ComposicionServicios` y el worker
de proyecciones en su `ConfiguracionMartenProjections{Dominio}`.

Tres proscripciones delimitan el alcance de esta decisión:

- **No se usa `MapEventType` ni se altera `EventNamingStyle`.** Registrar un tipo no redeclara su
  alias: Marten lo sigue derivando del nombre de clase. Este ADR no cambia la estrategia de
  identificación, solo garantiza que el mapping esté presente.
- **No se registra ningún tipo con el nombre calificado antiguo** (ni clase shim, ni un
  `MapEventType` al viejo). Eso invertiría la tolerancia descrita arriba: Marten encontraría un
  mapping alternativo para el `mt_dotnet_type` obsoleto y deserializaría las filas viejas al tipo
  viejo, que es exactamente lo que no se quiere.
- **Un evento que solo cruza el bus no entra en la lista.** `PublicEvents` y `PrivateEvents` se
  deserializan a un tipo fijo por endpoint y nunca pasan por el `EventGraph`. El criterio de
  inclusión es el mismo del ensamblado (decisión #1): se persiste. `MarcacionRegistrada` entra por
  ser persistida, no por ser `IPrivateEvent`.

Dos guardrails sostienen la decisión, ambos sin Postgres (`AllKnownEventTypes()` es cálculo en
memoria): uno verifica que los tipos estén registrados en el store real que compone cada lado, y otro
**congela el alias contra literales** (`turno_creado`, `marcacion_registrada`, ...), de modo que el
día en que un rename de clase cambie la identidad de un evento ya persistido se vea en la suite, no
en producción.

### 7. MEF-ADR-0039 es el canon del marco; este ADR es su aplicación local

*Decisión incorporada por el issue #317.*

MEF-ADR-0039, del harness (`eda-evsourcing-azure-harness`), generaliza la partición de ensamblados
de eventos por rol como composición canónica para todo Bounded Context que el marco scaffoldea.
Este ADR es la **aplicación local** de ese canon en este repo, no una fuente independiente de la
regla: cuando este ADR y MEF-ADR-0039 coincidan, es porque este ADR fue la fuente de referencia
empírica que informó al marco (ver "Referencias" de MEF-ADR-0039); cuando difieran, **gana el
marco**, y la divergencia se paga en este repo, documentada como deuda abierta en "Negativas y deuda
asumida" hasta que un issue local la resuelva.

Que "gana el marco" es una regla que **este repo adopta por decisión propia**, no una obligación que
el marco imponga: MEF-ADR-0039 decisión #9 fija su alcance como *greenfield-only* y declara la
migración de consumidores existentes **no-objetivo explícito** -- cada consumidor decide cuándo el
costo se justifica. Esta enmienda y sus issues hermanos (#318, #319, #320) **son** esa decisión local
de migrar, tomada aquí. Lo que la regla anti-divergencia fija es la dirección: el destino de la
composición lo define el canon, y este repo no mantiene una variante propia en competencia.

Esta regla anti-divergencia no es retroactiva a las decisiones que este ADR ya fijó y que
MEF-ADR-0039 no contradice: la partición por rol (decisión #1), las listas de serialización por
dominio (decisión #4) y la identidad del evento por alias (decisión #6) coinciden con el canon del
marco y no requieren cambio. Donde el marco divergió de la versión original de este ADR -- el grafo
encadenado de la decisión #2 -- esta misma enmienda ya reescribió la decisión local para alinearla.

## Alternativas consideradas

**Mover el modelo de dominio completo (CA-ADR-0028 decisión #1).** Descartada por innecesaria:
ningún consumidor pide los aggregates ni los value objects de cálculo, y meterlos obligaría al worker
a cargar la lógica de cálculo de horas laborales que nunca usa. Además diluye el criterio de
inclusión, que es lo que este ADR aporta.

**Un `DomainEvents` compartido entre dominios.** Descartada: ControlHoras podría compilar contra
`TurnoCreado` y nada lo detendría. Con uno por dominio, la autonomía la impone el compilador.

**Un Shared Kernel para el modelo rico común.** Evaluada y descartada con evidencia: no existe ningún
tipo rico usado por ambos dominios. El candidato, `FranjaOrdinaria`, resultó exclusivo de
Programación; ControlHoras usa la versión plana `DetalleFranjaOrdinaria` que le llega dentro del
evento, que es el patrón correcto de EDA y ya funcionaba.

**Mover el comando `CrearTurno` a `DomainEvents`** junto con su evento. Descartada: habría metido un
DTO de entrada HTTP en un ensamblado cuyo criterio es "se persiste", justo lo que este ADR busca
evitar.

**Que el factory reciba `FranjaOrdinaria` ya construidas.** Descartada por cambio de comportamiento:
hoy el factory acumula en un solo `AggregateException` los errores de nombre, franjas y solapamiento;
si el caller construye los value objects, la primera excepción corta la acumulación.

**Reusar `DetalleFranjaOrdinaria` como entrada del factory.** Descartada: su forma no calza (declara
`DiaOffsetFin` explícito, que los factories de los value objects calculan internamente) y es el DTO
de **salida** hacia ControlHoras, no de entrada.

## Consecuencias

### Positivas

- El worker alcanza los 5 eventos y ambas listas de serialización con solo dos `ProjectReference`.
  Verificado con un spike descartable: el source generator de Marten emitió el dispatcher
  `[GeneratedEvolver]` despachando los cinco eventos, lo que descarta además el fallo en runtime por
  ausencia del analizador en el ensamblado del `partial`.
- Cada criterio de inclusión es verificable mecánicamente, y `reviewer` puede aplicarlo.
- `PublicEvents` queda sin dependencias de proyecto, condición para empaquetarlo como NuGet sin
  filtrar eventos internos.

### Negativas y deuda asumida

- ~~**Los streams de dev creados antes del refactor quedan ilegibles.**~~ **Deuda pagada por el
  issue #277 (decisión #6).** La primera versión de este ADR asumió la pérdida y mandaba purgar los
  schemas `programacion` y `control_horas` de dev al desplegar, porque sin ningún tipo registrado toda
  lectura dependía del fallback por `mt_dotnet_type` --que apunta a un assembly donde el tipo ya no
  existe-- y lanzaba `UnknownEventTypeException`. Esa purga **nunca se ejecutó** y el código sí se
  desplegó, así que el defecto quedó armado (lo dispara la primera rehidratación de un aggregate
  preexistente). El registro explícito de la decisión #6 lo corrige **retroactivamente y sin tocar
  datos**: como el alias resuelve, la columna obsoleta se ignora. La purga queda revertida como
  estrategia -- no era necesaria, y de aquí en adelante el registro explícito es precondición de todo
  movimiento futuro de namespace o assembly, no la purga.
- ~~**`MarcacionRegistrada` queda en `DomainEvents` siendo `IPrivateEvent`.** Se publica al topic
  `marcacion-registrada` y además se persiste, pero es un tipo rico y un tipo rico no puede vivir en
  un ensamblado cuyo contrato es que todo lo suyo cruza un bus siendo plano. Su aplanamiento y
  traslado a `PrivateEvents` es el issue #270.~~ **Deuda pagada por el issue #270**, pero no por la
  vía que este ADR había anticipado (aplanar `MarcacionRegistrada` para moverla a `PrivateEvents`).
  La investigación empírica de #270 mostró que `MarcacionRegistrada` tiene ctor **público**
  parametrizado --el rol de bus le arrancó el encapsulamiento al evento de dominio-- así que
  aplanarla no habría ganado nada; ya era, de facto, un tipo sin invariantes que el bus podía
  deserializar. La resolución real fue **un tipo por rol, con nombres simples distintos**:
  `MarcacionRegistrada` se queda en `ControlHoras.DomainEvents`, deja de implementar `IPrivateEvent`
  y recupera el sentido de su `ConfigurarSerializacion` (antes redundante por el ctor público); el
  contrato de bus es un tipo nuevo, `RegistroDeMarcacionCreado` (`record` plano, `PrivateEvents.ControlHoras`),
  con paridad de campos pero **nombre simple deliberadamente distinto**. La razón del nombre distinto
  es estructural, no estilística: el Function App de `ControlHoras` referencia ambos ensamblados, y
  si los dos tipos compartieran nombre simple, un `using` equivocado publicaría el evento rico al bus
  sin que el compilador lo detectara --exactamente la regresión que CA-ADR-0025 existe para impedir--.
  El precedente de forma ya existía en el propio repo (`ProgramacionTurnoSolicitada` en
  `Programacion.DomainEvents` frente a `ProgramacionTurnoDiarioSolicitada` en
  `PrivateEvents.Programacion`); #270 lo generaliza como la resolución estándar para todo evento con
  doble rol.
- **Los value objects de cálculo mantienen su registro de serialización en la Function App**
  (`ConfiguracionSerializacionCalculoHoras`), aunque hoy no se persistan. El criterio original del
  issue los daba por "registros sin efecto"; resultó falso: sostienen la barrera de #232 CA-5, que
  hace round-trip de `IntervaloTemporal` contra el `ISerializer` que compuso el contenedor para
  detectar que un edit del bloque `ConfigureMarten` tumbe la serialización en silencio. No hay canario
  de reemplazo, porque los tres eventos tienen constructor público y STJ vanilla los deserializa
  igual **en las condiciones en que hoy se ejercitan** (round-trip contra `CrearOpcionesMarten()`,
  PascalCase + `PropertyNamingPolicy = null`). **Corrección factual (issue #270): esa afirmación,
  leída como "STJ sin resolver los deserializa igual en cualquier canal", es falsa.** La verificación
  empírica de #270 (proyecto descartable, .NET 10, réplica exacta de la forma actual: ctor público
  parametrizado + ctor privado + propiedades `private set`) encontró tres resultados distintos según
  las opciones del canal:

  | Escenario | Resultado |
  |---|---|
  | PascalCase + `PropertyNamingPolicy = null` (Marten) sin resolver | round-trip completo |
  | camelCase + `PropertyNameCaseInsensitive = true` (`ServiceBusDeserializador`) | round-trip completo |
  | camelCase + opciones STJ estrictas (sin case-insensitive) | **pérdida silenciosa**: sin excepción, los 4 campos quedan en su valor default |

  La portabilidad de estos tipos por el bus interno no la sostiene la forma del tipo (ctor público),
  sino el `PropertyNameCaseInsensitive = true` que `ServiceBusDeserializador` fija en el consumidor.
  El modo de fallo del tercer escenario es **silencioso, no una excepción** --el guardrail que lo
  detectaría es un round-trip con opciones STJ estrictas, no el round-trip contra `CrearOpcionesMarten()`
  que ya corre--. La lista de eventos --la que el worker replica-- sí quedó limpia de ellos.
- `IgualdadTestBase` se duplica en cada proyecto de tests que lo usa (4 copias), en vez de extraerse a
  un proyecto compartido.
- Los filtros `paths` de los tres workflows de deploy deben enumerar los ensamblados nuevos. Sin eso,
  un cambio en un evento no dispara el despliegue del proceso que lo consume: la misma staleness
  silenciosa que esos workflows ya documentaban para `global.json`.
- **El grafo de referencias entre ensamblados de eventos todavía es el encadenado, no las tres islas
  que la decisión #2 fija desde esta enmienda (issue #317). Deuda abierta, sin pagar.** Evidencia
  concreta verificada en este repo: `PrivateEvents.csproj` tiene `<ProjectReference>` hacia
  `PublicEvents.csproj`; `ControlHoras.DomainEvents.csproj` y `Programacion.DomainEvents.csproj`
  tienen `<ProjectReference>` hacia ambos ensamblados de bus; `TurnoDiarioAsignado` (persistido)
  embebe `InformacionEmpleado` (`PublicEvents`) y `DetalleTurno` (`PrivateEvents`) como payload
  anidado; `ProgramacionTurnoSolicitada` (persistido) hace lo mismo;
  `ProgramacionTurnoDiarioSolicitada` (bus privado) usa `InformacionEmpleado` (`PublicEvents`); y
  `FranjaOrdinaria.ToDetalle()`/`SubFranja.ToDetalle()` retornan
  `DetalleFranjaOrdinaria`/`DetalleSubFranja`, tipos de `PrivateEvents`. El retiro de la referencia
  `PrivateEvents -> PublicEvents` es el issue #318; el retiro de las referencias de ambos
  `DomainEvents` hacia los buses y la duplicación de payload por rol que las reemplaza, el issue
  #319; el enforcement mecánico con tests de arquitectura que impida que el grafo regrese al
  encadenado, el issue #320. Esta enmienda fija la doctrina primero, a propósito: no afirma un
  cumplimiento que todavía no existe -- mismo aprendizaje que la purga nunca ejecutada que este ADR
  ya documenta más arriba.
- **El worker alcanza `PublicEvents`/`PrivateEvents` transitivamente vía `ReadModels`, y ningún issue
  hermano lo paga. Deuda abierta, propia de esta enmienda.** `ReadModels.csproj` referencia ambos
  buses desde el issue #289 CA-1 --decisión deliberada y justificada en su momento: `TurnoDiarioView`
  reusa `InformacionEmpleado` (`PublicEvents`) y `DetalleTurno` (`PrivateEvents`) porque son
  exactamente los DTOs planos destinados a cruzar fronteras, y el paquete de markers no arrastra
  dependencias--. Como el worker referencia `ReadModels`, los dos ensamblados de bus le llegan por
  transitividad, lo que cumple la letra de la decisión #2 (`ReadModels` no es un ensamblado de eventos,
  así que el cero `<ProjectReference>` no lo alcanza; el worker no declara ninguna referencia directa
  a un bus) pero no su propósito declarado en el canon: "no arrastrar transitivamente
  `PrivateEvents`/`PublicEvents` a un proceso que no los necesita" (MEF-ADR-0039 decisión #2).
  Resolverlo exigiría que `ReadModels` redeclare esos dos DTOs como records propios --el mismo patrón
  de duplicación por rol de la decisión #5, aplicado al read-side--, y eso **revisaría** una decisión
  vigente (#289 CA-1), no solo pagaría deuda: queda fuera del alcance de #318 (retiro
  `PrivateEvents -> PublicEvents`), #319 (payload por rol en los `DomainEvents`) y #320 (tests de
  arquitectura sobre los tres ensamblados y el worker). Sin issue asignado todavía; se registra aquí
  para que #320 no escriba su suite asumiendo que el worker ya está limpio.

## Referencias

- CA-ADR-0001 (Function App por dominio): la autonomía que esta estructura vuelve verificable por
  compilación.
- CA-ADR-0002 (Contracts para eventos y value objects compartidos): **superado**. El proyecto que
  describe fue eliminado; su criterio "lo compartido" se reemplaza por los tres criterios de rol.
- CA-ADR-0025 (el modelo rico no cruza el bus): premisa de la partición `PublicEvents`/`PrivateEvents`
  frente a `DomainEvents`. Se refuerza, no se cambia.
- CA-ADR-0028 (biblioteca de dominio como frontera write/read): **superado**. Su decisión #1 mandaba
  mover aggregates y value objects de cálculo, ampliación que la investigación mostró innecesaria.
  Contenía además un error factual: afirmaba que ninguno de los 5 eventos implementaba
  `IPublicEvent`/`IPrivateEvent`, cuando `MarcacionRegistrada` sí implementaba `IPrivateEvent`. Desde
  el issue #270 la afirmación es cierta por otra vía: ningún evento persistido lleva marker de bus.
- MEF-ADR-0012 (encapsulamiento, `ConfigurarSerializacion`, ctor privado): los tipos ricos que se
  mudaron siguen este patrón sin cambios.
- MEF-ADR-0023 (lo que cruza un bus debe ser plano y portable): criterio de inclusión de
  `PublicEvents`/`PrivateEvents`.
- MEF-ADR-0034 sección 5 (worker de proyecciones): fija que las clases de proyección viven en el
  worker y que este no puede alcanzar un Function App, la restricción que origina todo el refactor.
- MEF-ADR-0039 (marco, composición canónica de ensamblados por rol del evento): **canon** del que
  este ADR es aplicación local (decisión #7). Generaliza la partición por rol (decisión #1 de este
  ADR) y sustituye el grafo encadenado de la versión original de la decisión #2 por tres islas con
  cero `<ProjectReference>`; generaliza también la decisión #5 de este ADR como payload por rol.
  Cuando el marco y este repo difieran en composición de ensamblados de eventos, gana el marco y la
  divergencia se paga aquí.

## Control de cambios

- 2026-07-31: creación. Fija la partición de los ensamblados de eventos por rol (`PublicEvents`,
  `PrivateEvents`, `{Dominio}.DomainEvents`), sus tres criterios de inclusión, el sentido de las
  dependencias permitidas y las reglas que el grafo de compilación garantiza. Supera a CA-ADR-0002 y
  CA-ADR-0028. Ejecutado en el issue #237.
- 2026-07-31: enmienda (issue #277). Agrega la decisión #6: el alias --derivado del nombre simple de
  la clase-- es la identidad del evento persistido, y cada ensamblado de eventos aloja la lista de sus
  tipos persistidos (`IdentidadEventos{Dominio}.TiposPersistidos`) que write-side y read-side registran
  con `Events.AddEventTypes(...)`. Proscribe `MapEventType`, alterar `EventNamingStyle` y registrar el
  nombre calificado antiguo. Marca como pagada la primera deuda de "Negativas y deuda asumida" y
  revierte la purga de streams como estrategia de mitigación.
- 2026-07-31: enmienda (issue #270). Marca como pagada la deuda de `MarcacionRegistrada` en "Negativas
  y deuda asumida", documentando que la resolución real fue separar **un tipo por rol** (el evento de
  dominio persistido conserva su nombre; el contrato de bus nuevo, `RegistroDeMarcacionCreado`, lleva
  un nombre simple deliberadamente distinto para que un `using` equivocado no compile) en vez de
  aplanar el tipo existente como se había anticipado. Corrige factualmente la afirmación "STJ vanilla
  los deserializa igual": cierto para PascalCase sin resolver (Marten) y para camelCase
  case-insensitive (`ServiceBusDeserializador`), falso para camelCase estricto --el modo de fallo es
  pérdida silenciosa de datos, no una excepción-- y depende del `PropertyNameCaseInsensitive = true`
  del consumidor, no de la forma del tipo.
- 2026-08-05: enmienda (issue #317). Reescribe la decisión #2: de grafo encadenado (`PublicEvents <-
  PrivateEvents <- {Dominio}.DomainEvents <- Function App`) a **tres islas** con cero
  `<ProjectReference>` cada una, ni entre ellas ni hacia ningún otro proyecto del repo; el Function
  App de cada dominio referencia los tres directamente y el worker de proyecciones solo
  `{Dominio}.DomainEvents` + `ReadModels`. Generaliza la decisión #5 con la regla de **payload por
  rol**: un tipo de payload no cruza ensamblados de eventos, con el mapeo entre bus y event store
  concentrado en el Function App. Alinea la cuarta regla de la decisión #3 con MEF-ADR-0039 decisión
  #7: la referencia única se declara explícita también para `PrivateEvents.Tests`, no solo para
  `PublicEvents.Tests`. Agrega la decisión #7: MEF-ADR-0039 (marco) es el canon, este ADR es su
  aplicación local, con regla anti-divergencia explícita ("gana el marco"). Registra como deuda
  **abierta** (sin pagar) el estado real del grafo de referencias de este repo, con evidencia
  concreta (`PrivateEvents.csproj -> PublicEvents.csproj`; ambos `DomainEvents` hacia los dos buses;
  `TurnoDiarioAsignado`, `ProgramacionTurnoSolicitada`, `ProgramacionTurnoDiarioSolicitada`,
  `FranjaOrdinaria`/`SubFranja`) y puntero a los issues que la pagan (#318, #319) y la congelan con
  tests de arquitectura (#320). Registra además una segunda deuda abierta, sin issue asignado: el
  worker alcanza ambos buses **transitivamente** vía `ReadModels` (issue #289 CA-1), lo que cumple la
  letra de la decisión #2 pero no el propósito del canon. Aclara que la cuarta regla de la decisión #3
  es un guardrail de diseño que el grafo **no** garantiza (la vía deliberada queda abierta; su
  enforcement es el issue #320) y que "gana el marco" es una adopción local, no una imposición del
  marco, que declara la migración de consumidores existentes no-objetivo (MEF-ADR-0039 decisión #9).
  Enmienda puramente doctrinal: no modifica ningún `.csproj` ni código.
