# CA-ADR-0031: Identidad de streams por store -- heuristica de anatomia y registro

## Estado

Aceptado.

## Contexto

MEF-ADR-0037 fija, por aggregate, el punto unico de conversion de una identidad de stream a
string y su formato canonico (Guid "D" en minusculas, o clave compuesta via
`ComputarStreamId(...)`). MEF-ADR-0043 seccion 1 agrega, encima de eso, la precondicion de que
ese string sea URL-safe cuando viaja como segmento de ruta. Ninguno de los dos ADRs dice nada
sobre una pregunta distinta: **si dos aggregates diferentes que comparten el mismo store llegan a
producir, por su propia anatomia, el mismo string para dos entidades distintas**.

Esa pregunta no es hipotetica en este BC. CA-ADR-0007 aprovisiona un unico servidor PostgreSQL con
un schema de Marten por dominio -- no por aggregate --, asi que un schema puede alojar varios
aggregates en el mismo store. El schema `control_horas` ya aloja hoy dos:
`RegistroDeMarcacionAggregateRoot` (identidad `{CodigoColaborador}:{Timestamp:yyyy-MM-ddTHH:mm:ss}`,
`RegistroDeMarcacionAggregateRoot.cs:25-26`) y `ControlDiarioAggregateRoot` (identidad
`{CodigoColaborador}:{Fecha:yyyy-MM-dd}`, `ControlDiarioAggregateRoot.cs:74-75`) -- y el issue #425
va a agregar un tercero, `DiaCalculado`, con identidad logica **colaborador+fecha**: exactamente la
misma pareja de componentes que ya identifica a `ControlDiarioAggregateRoot`. Si `DiaCalculado`
hubiera heredado la misma anatomia sin prefijo (`{codigo}:{fecha}`), habria producido el mismo
string que un `ControlDiario` ya existente del mismo colaborador y la misma fecha -- dos entidades
de negocio distintas reclamando la misma fila.

**Verificado que Marten no discrimina por tipo de aggregate dentro de un store** (decompilacion /
lectura de fuente propia contra Marten 9.12.0, la version que pinea MEF-ADR-0003):

- `Marten.Events.Schema.StreamsTable` (`StreamsTable.cs:29-39`) construye la primary key de
  `mt_streams` con, en orden, la columna `tenant_id` (solo si `TenancyStyle.Conjoined`, el caso de
  este BC -- CA-ADR-0027) y la columna `id` (el string que produce `ComputarStreamId` o el Guid).
  La columna `type` (`AggregateTypeName`) se agrega con `AllowNulls()` y **no** entra en la PK: es
  metadata, no un discriminador de unicidad.
- `Marten.Events.Operations.InsertStreamBase` (`InsertStreamBase.cs:47-79`) traduce una violacion de
  unicidad sobre `mt_streams` (`SqlState: PostgresErrorCodes.UniqueViolation`) en
  `ExistingStreamIdCollisionException` -- el mismo camino de error tanto si el string colisiona
  contra un stream del mismo tipo de aggregate como si colisiona contra uno de un tipo distinto.

Es decir: la PK de `mt_streams` es `(tenant_id, id)`, nunca `(tenant_id, id, type)`. Dos aggregates
del mismo store que computen el mismo string para casos distintos no generan una excepcion nueva y
legible -- generan la misma colision que dos streams legitimos del mismo aggregate, indistinguible
sin inspeccionar el codigo que los origino.

Sin una heuristica que fuerce la disjuncion por construccion, cada `Id` de `AggregateRoot` nuevo se
evalua "a ojo" contra sus vecinos de store -- el modo de falla que casi ocurrio con `DiaCalculado`
frente a `ControlDiario`, descubierto durante la sesion de aprobacion del 2026-08-17 y su correccion
de rumbo del 2026-08-18/19.

### Que queda fuera de este ADR

La correccion de rumbo del 2026-08-18/19 elimino del alcance original el charset URL-safe de las
claves: la URL-safety es propiedad de los **segmentos de ruta** (MEF-ADR-0043 seccion 1.1/1.2), no
de la clave de stream en si -- una clave de stream que nunca viaja completa por una URL (el caso de
"varios componentes" de la seccion 1 de este ADR) no tiene por que respetar ese charset. La
renotacion de `RegistroDeMarcacion`/`ControlDiario` (issues #419/#420) se motiva por la ausencia de
prefijo -- el problema que este ADR resuelve --, no por el charset de sus componentes actuales.

## Decision

### 1. Deslinde URL vs clave: la unidad es el componente tipado

MEF-ADR-0037 seccion 2 ya distingue, para el borde HTTP, entre una identidad de un solo componente
(Guid, o un value object unico como `Identificacion`) y una clave natural compuesta. Este ADR fija
la lectura operativa de esa distincion en terminos de **cuantos componentes viajan por segmento de
ruta**:

- **Identidad de UN componente**: su forma string viaja como segmento unico de la URL y coincide
  con la clave (o deriva a ella via el punto unico de conversion). Precedente conforme ya
  desplegado: `ObtenerFichaColaborador.FunctionEndpoint` recibe `{id}` = `Identificacion.ToString()`
  (`"CC-79543210"`), lo parsea una vez con `IdentificacionDeRuta.TryParsear` (400 explicito si no
  parsea) y computa `ColaboradorAggregateRoot.ComputarStreamId(identificacion)` -- el mismo string
  que la clave. Al viajar, ese componente **si** queda sujeto a la precondicion URL-safe de
  MEF-ADR-0043 seccion 1.1/1.2 (que `Identificacion` ya cumple: charset `PILA-Numero`).
- **Identidad de VARIOS componentes**: viajan descompuestos, un componente tipado por segmento; la
  clave concatenada **jamas** viaja completa (proscripcion de MEF-ADR-0037 seccion 2: "un parametro
  de ruta `string` cuyo valor viaje sin parseo... proscrito", aplicado aqui a la clave ya armada).
  Esto fija la forma de las rutas de #429: `.../{codigo}/dias/{fecha}`, nunca
  `.../{codigo}:{fecha}`.
- El **prefijo de tipo y el separador son internos** a `ComputarStreamId` -- construccion que ocurre
  del lado del servidor, despues de que los componentes tipados ya llegaron por separado -- y
  **jamas aparecen en una URL**, en ningun caso.

Esta lectura cierra la pregunta abierta de las field notes de la sesion de aprobacion del
2026-08-17 sobre si la clave completa (con su prefijo y separador) debia o no ser URL-safe: no
aplica, porque la clave completa nunca es el valor de un segmento de ruta.

### 2. Heuristica de anatomia (aplicada por aggregate, en el issue que lo crea)

1. **¿Necesita identidad natural** (idempotencia determinista o convergencia de eventos)? Si no ->
   Guid canonico (MEF-ADR-0037 seccion 1), sin prefijo. Fin del test. Precedente: `CatalogoTurnos` y
   `SolicitudProgramacionAggregateRoot` (`CatalogoTurnos.cs:20`, `Id = evento.TurnoId.ToString();`)
   -- ninguno de los dos necesita converger por contenido, asi que un Guid basta.
2. **Prefijo obligatorio**: iniciales del `AggregateRoot` en minusculas. Precedente industrial:
   Stripe prefija sus object ids con las iniciales del tipo de recurso (`cus_` para `Customer`,
   `ch_` para `Charge` -- verificado contra la documentacion oficial, que muestra ids con la forma
   `cus_...` en el objeto `Customer` [1]; la convencion completa de prefijos por tipo esta
   documentada de forma independiente [2]). El prefijo disjunta por construccion los aggregates
   dentro de un mismo store: dos aggregates con prefijos distintos no pueden colisionar aunque el
   resto de sus componentes coincida.
3. **Componentes del dominio (fechas/timestamps) siempre en ISO 8601 basico**: `yyyyMMdd` /
   `yyyyMMddTHHmmss` -- normalizacion BC-wide decidida el 2026-08-18, solo `[0-9]` y `T`, limpios
   para cualquier separador no alfanumerico del formato de entrada, parseables con `ParseExact` en
   .NET 10 (verificado empiricamente durante esa sesion). Componentes de tercero (p. ej.
   `CodigoColaborador`) **no se alteran jamas**: su charset lo garantiza la invariante del borde
   (MEF-ADR-0043 seccion 1.2, caso "identificador asignado por un tercero").
4. **Separador**: un caracter fuera del charset de TODOS los componentes de esa clave. Decision caso
   por caso del issue que crea la clave; `:` es el candidato por defecto -- legible, precedente ya
   vigente en `ControlDiarioAggregateRoot`/`RegistroDeMarcacionAggregateRoot`, y el que MEF-ADR-0037
   seccion 1 ya usa en su propio ejemplo (`$"{empleadoId}:{fecha:yyyy-MM-dd}"`). La invariante "el
   componente no contiene el separador" acompana cada caso -- precedente ya escrito en
   `RegistroDeMarcacionAggregateRoot.EsComponenteValidoDeStreamId` (`RegistroDeMarcacionAggregateRoot.cs:35-36`).
5. **Test de split simple**: `clave.Split(separador)` debe devolver exactamente los componentes,
   siempre. Si no, la anatomia esta mal disenada -- este test es mecanico y verificable en un test
   unitario por aggregate, sin Postgres.
6. **Registrar la anatomia en el registro del store** (seccion 3) -- este ADR se enmienda cada vez
   que un issue crea un aggregate nuevo o migra uno existente.

### 3. Registro de anatomias por store: primero-llega-primero-se-sirve

Dentro de un store (= schema de Marten, CA-ADR-0007), cada anatomia registrada es exclusiva de su
aggregate. En colision (dos aggregates candidatos a la misma forma), la clave **nueva** deriva --
tipicamente ajustando el prefijo o el separador -- y **una clave ya registrada jamas cambia** por
la llegada de un vecino. Cambiar una clave ya desplegada solo ocurre via un issue de migracion
propio, con la disciplina de dos despliegues (o purga en el mismo despliegue, si el entorno no tiene
streams reales que preservar) que ya fija MEF-ADR-0036 seccion 5 para el caso analogo de mover o
renombrar la identidad de un **evento** persistido -- el mismo tipo de riesgo (dato ya persistido
cuya clave de resolucion cambia) aplicado aqui a la clave de **stream** en vez de al alias del
evento.

### 4. Registro inicial (los 3 stores del BC)

| Store (schema) | Aggregate | Anatomia vigente | Anatomia objetivo | Notas |
|---|---|---|---|---|
| `control_horas` | `RegistroDeMarcacionAggregateRoot` | `{codigo}:{yyyyMMddTHHmmss}` extendido, sin prefijo (legado) | `rdm:{codigo}:{yyyyMMddTHHmmss}` | Objetivo del issue #419. La forma legada queda registrada como legado hasta que #419 ejecute su migracion (protocolo MEF-ADR-0036 seccion 5). |
| `control_horas` | `ControlDiarioAggregateRoot` | `{codigo}:{yyyyMMdd}`, sin prefijo (legado) | `cd:{codigo}:{yyyyMMdd}` | Objetivo del issue #420. Misma disciplina de migracion que la fila anterior. |
| `control_horas` | `DiaCalculado` (nace en #425) | -- (no existe todavia como aggregate persistido) | `dc:{codigo}:{yyyyMMdd}` | Nace directamente en notacion objetivo -- ningun issue de migracion necesario. El prefijo `dc:` es lo que evita la colision con `ControlDiario` que motiva este ADR (misma identidad logica colaborador+fecha, mismo store). |
| `colaboradores` | `ColaboradorAggregateRoot` | `{Tipo}-{Numero}` (contrato de `Identificacion.ToString()`, ej. `CC-79543210`) | Sin cambio | Vigente sin prefijo: decision del experto de dominio, 2026-08-19. Es identidad de UN componente que ya viaja en URL (ya URL-safe, `ObtenerFichaColaborador`), el store de Colaboradores tiene un solo aggregate (nada que prevenir todavia), y renotar exigiria migracion + rebuild de `FichaColaborador` (worker de proyecciones) sin comprar ninguna disjuncion real. |
| `programacion` | `CatalogoTurnos`, `SolicitudProgramacionAggregateRoot` | Guid canonico "D" | Sin cambio | Paso 1 de la heuristica: ninguno de los dos necesita identidad natural. Sin registro adicional -- un Guid nunca colisiona con otro Guid de otro aggregate por construccion (espacio de valores, no de forma). |

La fila de Colaboradores es una **excepcion deliberada** al paso 2 de la heuristica (prefijo
obligatorio), no una violacion: el paso 2 existe para disjuntar vecinos dentro de un store, y hoy no
hay ningun vecino que prevenir en `colaboradores`. Si un segundo aggregate llegara a ese store con
riesgo de colision de identidad, esa fila se re-evalua bajo la regla de "primero-llega-primero-se-
sirve" de la seccion 3 -- la clave existente (`Identificacion.ToString()`) no cambia; el vecino
nuevo deriva su propio prefijo.

## Alternativas consideradas

**Guid canonico para todo aggregate, incluidos los que hoy usan clave natural.** Descartada:
rompe la idempotencia determinista de `RegistroDeMarcacionAggregateRoot` (que depende de que dos
mensajes con el mismo `CodigoColaborador`+timestamp resuelvan al mismo stream para detectar
duplicado exacto, `RegistroDeMarcacionAggregateRoot.cs:8-9`) y la convergencia de eventos de
`ControlDiarioAggregateRoot` (dos comandos del mismo colaborador+fecha deben converger al mismo
stream). Un Guid nace aleatorio; no puede servir ese proposito sin abandonar la idempotencia que la
clave natural existe para dar.

**Helper de runtime que centralice o valide la anatomia** (una extension o atributo del harness
que garantice disjuncion entre aggregates de un store). Descartada bajo el mismo criterio que ya usa
MEF-ADR-0037 seccion 5 (Rule of Three, MEF-ADR-0018): hoy hay tres stores, ninguno con mas de tres
aggregates, y ningun sitio divergente reclama todavia una abstraccion compartida. El sitio natural
de un helper asi seria el paquete `Cosmos.EventSourcing.CritterStack`, no este harness ni este repo.

**Prefijo por store completo en vez de por aggregate** (por ejemplo, un unico prefijo
`controlhoras:` para todo lo que cae en ese schema). Descartada: no resuelve la disjuncion entre
vecinos, solo la traslada un nivel arriba -- `ControlDiario` y `DiaCalculado` seguirian colisionando
dentro de ese mismo prefijo compartido, exactamente el problema que este ADR existe para cerrar.

**Un separador fijo unico para todo el BC, sin evaluacion caso por caso.** Descartada: la seccion 2
paso 4 ya deja `:` como default, pero fijarlo como obligatorio congelaria la anatomia frente a un
componente futuro que si contuviera `:` en su charset (un caso hipotetico hoy, pero no descartable
para un value object de tercero que este ADR no controla). El costo de evaluar el separador caso por
caso es bajo (un paso mas del test de la seccion 2) frente al costo de una regla que no puede
adaptarse.

## Consecuencias

### Positivas

- Cierra la laguna real detectada en la sesion del 2026-08-17: un aggregate nuevo que comparte store
  con vecinos ya no se evalua "a ojo" -- sigue un test de seis pasos verificable, con un ejemplo de
  cada paso ya presente en el codigo del repo.
- El paso 5 (test de split simple) da un criterio mecanico, expresable como test unitario por
  aggregate sin necesitar Postgres -- mismo espiritu que los guardrails "en memoria" que ya usan
  MEF-ADR-0036 seccion 4 y MEF-ADR-0037 seccion 5 para sus propios modos de falla.
- La regla de registro (seccion 3) dirime colisiones futuras sin reabrir codigo ya desplegado: la
  clave nueva deriva, la existente no se toca salvo por un issue de migracion propio.
- El deslinde de la seccion 1 (unidad = componente tipado) cierra la pregunta abierta de las field
  notes del 2026-08-17 que afectaba el diseno de rutas de #429, y deja explicito que este ADR no
  reabre MEF-ADR-0037 seccion 2 ni MEF-ADR-0043 seccion 1 -- los aplica a un caso que ninguno de los
  dos cubria (identidad de VARIOS componentes en la ruta).
- La decision de Colaboradores queda documentada con su racional explicito (seccion 4), evitando que
  un futuro reviewer la marque como inconsistencia frente a la heuristica general.

### Negativas y deuda asumida

- **Sin test automatico de colision entre vecinos de un store.** A diferencia del paso 5 (split
  simple, verificable por aggregate en aislamiento), no hay un oraculo en memoria al que preguntarle
  "¿que anatomias ya usa este store?" sin leer el registro de la seccion 4 a mano -- mismo trade-off
  que MEF-ADR-0037 seccion 5 acepta para su propio modo de falla (doctrina + revision, no tipo ni
  test). Un aggregate nuevo que ignore el registro y elija una anatomia colisionante no lo detecta
  el compilador ni la suite; lo detecta `reviewer` o, en el peor caso, `ExistingStreamIdCollisionException`
  en produccion.
- **Las claves legadas de `RegistroDeMarcacionAggregateRoot` y `ControlDiarioAggregateRoot` (sin
  prefijo, timestamp/fecha extendidos) conviven con la notacion objetivo hasta que #419/#420
  ejecuten.** Este ADR fija el destino y el registro; no ejecuta la migracion. La migracion sigue el
  protocolo de dos despliegues (o purga en el mismo despliegue si el entorno no tiene streams reales
  que preservar) que MEF-ADR-0036 seccion 5 ya fija para el caso analogo de mover la identidad de un
  evento persistido.
- **La tension textual entre MEF-ADR-0043 seccion 1.1/1.2 y MEF-ADR-0037 seccion 1 sigue sin resolver
  en el marco** (reportada como harness#681). Este ADR sostiene localmente la lectura "la unidad es
  el componente tipado" (seccion 1) mientras el marco no la incorpore; la propuesta de llevar esa
  precision al canon es harness#682. Si el marco adopta una lectura distinta, la divergencia queda
  documentada aqui hasta que un issue local la reconcilie -- mismo patron que CA-ADR-0029 aplica
  frente a MEF-ADR-0039.
- **El registro del paso 6 depende de disciplina, no de enforcement mecanico.** Ningun test impide
  que un futuro issue cree un aggregate nuevo sin enmendar la tabla de la seccion 4; el gate es
  `planner`/`reviewer` verificando el ADR, igual que el resto de la doctrina de este marco que no
  tiene guardrail ejecutable (MEF-ADR-0037 seccion 5).

## Referencias

- **[1]** "The Customer object" -- Stripe API Reference: los ejemplos de objeto `Customer` muestran
  ids con la forma `cus_...`. https://docs.stripe.com/api/customers/object
- **[2]** "Stripe keys and IDs" -- catalogo de prefijos de Stripe por tipo de objeto (`cus_`
  Customer, `ch_` Charge, `sub_` Subscription, `in_` Invoice, `acct_` Account, entre otros), citado
  como precedente industrial de prefijo legible por tipo de recurso, no como fuente normativa de
  este ADR. https://gist.github.com/fnky/76f533366f75cf75802c8052b577e2a5
- Marten 9.12.0 (version pinneada por MEF-ADR-0003), lectura de fuente propia:
  `Marten.Events.Schema.StreamsTable` (`StreamsTable.cs:29-39`, PK de `mt_streams` = `tenant_id` +
  `id`; la columna `type`/`AggregateTypeName` es nullable y no integra la PK) y
  `Marten.Events.Operations.InsertStreamBase` (`InsertStreamBase.cs:47-79`, una violacion de
  unicidad sobre `mt_streams` se traduce siempre a `ExistingStreamIdCollisionException`,
  independientemente del tipo de aggregate involucrado) -- verificacion propia que sostiene "Marten
  no discrimina tipos en `mt_streams`" en el Contexto de este ADR.
- MEF-ADR-0037 (identidad de stream y su representacion string canonica): este ADR construye
  encima -- honra su seccion 2 con la lectura "la unidad es el componente tipado" (seccion 1 de este
  ADR) y no toca su punto unico de conversion (seccion 1 de ese ADR) ni su formato canonico Guid
  "D".
- MEF-ADR-0043 (doctrina HTTP de comandos), seccion 1 (precondicion URL-safe, charset RFC 3986,
  criterio rechazar-vs-normalizar): deslinde explicito en la seccion 1 de este ADR -- la URL-safety
  es propiedad de los segmentos de ruta, no de la clave de stream completa.
- MEF-ADR-0036 (identidad del evento persistido en el event store), seccion 5 (protocolo de dos
  despliegues / purga en el mismo despliegue para mover o renombrar identidad ya persistida):
  disciplina analoga que rige la migracion de las claves legadas de la seccion 4 de este ADR.
- MEF-ADR-0018 (heuristicas de evolucion y reuso, Rule of Three): fundamenta el descarte del helper
  de runtime en "Alternativas consideradas" -- tres stores, ningun tercer caso real que reclame la
  abstraccion todavia (el propio issue #425, tercer aggregate del store `control_horas`, es la
  ocurrencia que este ADR anticipa sin esperar a un cuarto caso).
- CA-ADR-0007 (PostgreSQL compartido con schemas separados por dominio): la causa estructural de que
  un store pueda alojar varios aggregates -- el schema es por dominio, no por aggregate.
- Issues #419 (renotacion `RegistroDeMarcacionAggregateRoot` a `rdm:...`), #420 (renotacion
  `ControlDiarioAggregateRoot` a `cd:...`), #425 (nace `DiaCalculado` en notacion objetivo `dc:...`),
  #429 (rutas `.../{codigo}/dias/{fecha}` que dependen de la seccion 1 de este ADR).
- harness#681 (tension textual reportada entre MEF-ADR-0043 1.1/1.2 y MEF-ADR-0037 seccion 1);
  harness#682 (propuesta de llevar la precision "unidad = componente tipado" al canon del marco).
- Field notes de la sesion de aprobacion, 2026-08-17, y su correccion de rumbo, 2026-08-18/19
  (origen de la pregunta abierta que cierra la seccion 1 de este ADR y del alcance descartado de
  charset URL-safe de claves).

## Control de cambios

- 2026-08-19: creacion (issue #417). Fija el deslinde URL vs clave (la unidad es el componente
  tipado), la heuristica de anatomia de seis pasos (identidad natural, prefijo obligatorio por
  iniciales del aggregate, componentes de fecha en ISO 8601 basico, separador fuera del charset de
  los componentes, test de split simple, registro en este ADR), la regla de registro por store
  (primero-llega-primero-se-sirve, la clave nueva deriva, la existente solo cambia con issue de
  migracion propio) y el registro inicial de los tres stores del BC (`control_horas`,
  `colaboradores`, `programacion`) con sus anatomias vigentes, legadas y objetivo. Verifica por
  lectura de fuente propia contra Marten 9.12.0 que la PK de `mt_streams` es `(tenant_id, id)` sin
  discriminar por tipo de aggregate, la causa raiz de la colision que este ADR previene. Deja fuera
  de alcance, por correccion de rumbo del 2026-08-18/19, el charset URL-safe de las claves (dominio
  de MEF-ADR-0043, no de este ADR).
