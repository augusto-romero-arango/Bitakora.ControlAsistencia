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

### 2. Sentido de las dependencias

```
PublicEvents                                  <- futuro paquete NuGet, sin dependencias de proyecto
   ^
   |
PrivateEvents                                 <- interno al BC
   ^                    ^
   |                    |
Programacion.DomainEvents   ControlHoras.DomainEvents      <- uno por dominio
   ^                            ^
   |                            |
Function App Programacion   Function App ControlHoras
   \                            /
    \____ Projections (worker) /                <- referencia ambos DomainEvents, ningún Function App
```

### 3. Tres reglas que garantiza el grafo de compilación, no la disciplina

- Un tercero que instale el paquete de `PublicEvents` **no puede** ver eventos internos del BC: la
  unidad de distribución de NuGet es el ensamblado completo.
- ControlHoras **no puede** compilar contra `TurnoCreado`, evento interno de Programación
  (CA-ADR-0001, autonomía de dominio). Un `DomainEvents` compartido no daría esta garantía.
- Nada en `PublicEvents` puede depender de un tipo interno, porque el grafo no lo permite.

Se agrega una cuarta, del lado de los tests: `PublicEvents.Tests` referencia **únicamente**
`PublicEvents`, de modo que si un test suyo llegara a necesitar `PrivateEvents` o un `DomainEvents`,
el compilador delata que el tipo bajo prueba no es distribuible.

### 4. Un ensamblado de eventos aloja la lista completa de serialización de su dominio

`ConfiguracionSerializacionProgramacion` y `ConfiguracionSerializacionControlHoras` viven en sus
respectivos `DomainEvents` y son la única fuente de la lista. El `ComposicionServicios` de cada
Function App las invoca en vez de declarar el resolver inline, y el worker puede invocar la misma
lista en su propio store. Antes del refactor esa lista cruzaba dos proyectos y la mitad de
Programación no existía como clase, lo que hacía imposible replicarla.

### 5. Los eventos no conocen los comandos

El factory de un evento persistido no recibe el comando que lo origina: recibe un tipo de entrada
propio del ensamblado de eventos. `TurnoCreado.Crear(Guid, string, IReadOnlyList<DatosFranja>)`, y el
comando `CrearTurno` --que se queda en la Function App con su `Franja` anidado, porque el contrato
HTTP no pertenece al ensamblado de eventos-- expone `ToDatosFranjas()` para traducirse. Un solo lugar
con ese mapeo, reusado por el handler y por sus tests.

La razón es estructural, no estética: `CrearTurno` vive en la Function App, que referencia
`Programacion.DomainEvents`, así que un factory que reciba el comando cierra un ciclo de referencias
y no compila.

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
