# CA-ADR-0034: Plantilla de turnos

## Estado

Aceptado (sesion de planeacion 2026-09-04; implementacion en #620-#629). Enmendado 2026-09-05 (refinamiento
de toda la cadena): tope de semanas, codificacion del dia, codigos HTTP e idempotencia (CA-ADR-0035), read-side
N1 + composicion en lectura (decision 5), turno inline diferido a #651. Enmendado 2026-10-06 (#886):
decisiones 2 y 5 -- la plantilla guarda una copia sincronizada de sus turnos y de los limites de su Jornada
(consistencia eventual con autocorreccion) y el cuadro vuelve a N1 con datos propios; implementacion en #887,
#888, #883, #884, #882, #867, #868 y #889. Enmendado 2026-10-27 (#1302; sesion planner 2026-10-26, cliente con
ciclos de 8 dias; #1280-#1300): el molde se generaliza a la **Plantilla de turnos**, un ciclo de dias 1..N
(N <= 42) que se aplica con una fecha de inicio. Cambian el titulo, la decision 1, la seccion "Aplicar una
plantilla" y la decision 6, y nace la seccion "Retiro de la plantilla semanal" (#1289); las decisiones 2 a 5
siguen vigentes con la generica como sujeto. Se enmienda y no se supera porque los issues #1280-#1300 citan
sus decisiones por numero (precedente #886).

## Contexto

El Turno es el plan de **un dia** (CA-ADR-0033: componible, editable, el atomo del dominio Programacion).
El 2026-08-29 el experto fijo que "mallas/rotaciones NO son conceptos del dominio sino recetas del cliente
(asistente MCP)", rechazo un "Periodo de Programacion" persistido y dejo la *Ventana de trabajo* como
contexto efimero de sesion. La tool `solicitar_programacion_turno` implementa exactamente esa doctrina:
ventana de fechas + **un** turno + N colaboradores, y el asistente compone lo demas llamandola varias veces.

Dos fuerzas reabren la decision:

1. **Reutilizacion con nombre.** Programar "Semana Cocina" (L-M-V un turno, Ma-J "Noche", S "Medio dia",
   D descanso) obliga hoy a 4 solicitudes por colaborador y por semana, sin que el patron quede en ningun
   lado. El experto quiere **crear una vez** esa composicion, **nombrarla** y **asignarla** despues. Lo que
   separa una receta del cliente de un concepto del dominio es precisamente eso: persistir + nombrar +
   reutilizar.
2. **El mapa de contexto ya lo prometia.** `docs/eda/context-map.yaml` describia Programacion como "ciclos y
   patrones semanales" desde el inicio; la decision de agosto lo dejo en contradiccion.

Fuera de la decision original quedaba **asignar** una plantilla a un colaborador; el experto lo saco
explicitamente del foco de la sesion de **construccion**. Aplicarla queda resuelto en la seccion
"Aplicar una plantilla" (enmienda 2026-10-04, #828).

## Decision

### 1. Plantilla de turnos: ciclo de dias 1..N con referencias al catalogo

Enmienda 2026-10-27 (#1302). Reemplaza la decision original (molde semanal de 1..6 semanas lunes-domingo).

- Aggregate `PlantillaTurnos` (stream por plantilla; Id Guid canonico del cliente -- paso 1 de CA-ADR-0031 --,
  nombre y numero de dias del ciclo fijado al crear: **N entre 1 y 42 inclusive**). Cada dia se identifica por
  **su numero en el ciclo (1..N)**, sin fechas, y referencia un turno del catalogo por `TurnoId`. El ciclo no
  conoce el calendario: cubre todo lo que se repite cada N dias (7, 14, 6x2, 4x4, 24x24, 14x14, 20x10, ciclos
  de 8 dias). Ruta: `programacion/plantillas-turnos`.
- **"Semanal" y "rotativa" son interfaces, no conceptos del dominio.** Son vistas del asistente (#1288, #1300)
  sobre este molde (decision 6). El dominio solo conoce el ciclo; el vocabulario "lunes/martes" y la clave
  `(semana, diaSemana)` con el VO `DiaSemana` de la modalidad semanal quedaron retirados (#1289).
- Se toma el nombre generico `PlantillaTurnos` a proposito: revierte el anti-squatting del 2026-09-04 porque el
  molde ciclico ES el general. El alias de un evento persistido es el nombre simple de la clase; una futura
  plantilla regida por el calendario (dia del mes, festivos) tomaria otro nombre.
- Eventos (`Programacion.DomainEvents`, registrados en `IdentidadEventosProgramacion`, CA-ADR-0029 #6):
  `PlantillaTurnosCreada`, `DiaDePlantillaTurnosAsignado`, `DiaDePlantillaTurnosQuitado`,
  `PlantillaTurnosRetirada`. Ninguno cruza bus.

### 2. Referencia por identidad + copia de valor sincronizada

Enmienda 2026-10-06 (#886). Reescribe la decision original ("referencia viva, no snapshot", 2026-09-04).

- Cada dia conserva `TurnoId` como identidad y guarda ademas una **copia de valor** `Turno` (VO rico, #887) con
  la **version del stream** del turno copiado. La plantilla guarda tambien la copia de los `LimitesJornada` de
  su Jornada (#867). `CatalogoTurnos` es un stream **por turno** (`Id = TurnoId`), no un catalogo unico; la
  Jornada es igualmente su propio stream.
- **Porque**: la plantilla se **audita a si misma** contra su Jornada (#868/#889) con objetos de dominio ricos
  (Tell-don't-Ask, MEF-ADR-0012) y emite sus advertencias en la misma transaccion que el comando que cambio su
  diseno. Esa regla necesita el turno y los limites **al decidir**. Se revierte el rechazo del 2026-09-04: su
  supuesto ("ningun caso de uso necesita el turno en la misma transaccion") ya no se cumple. El ADR original no
  estaba equivocado; decidio con ese supuesto.
- **Correccion de la cita**: MEF-ADR-0046 **no aplica**. Trata datos de *otro dominio*; turno, Jornada y
  plantilla son todos de Programacion. La frontera que obliga a sincronizar es la del **aggregate** (cada turno
  es su stream, cada Jornada el suyo): consistencia eventual entre aggregates del mismo dominio.
- **Mecanica**:
  - **Disparo**: los eventos de diseno del turno y `TurnoRetirado` (#883) y el cambio de limites de la Jornada
    (#884) publican un evento privado **plano** (CA-ADR-0025) con el estado completo resultante y la version
    del stream; la reaccion reinstancia el VO con sus factories y envia el comando de sincronizacion a cada
    plantilla que lo usa. Es reaccion + comando + evento propio por plantilla: `Apply()` solo rehidrata desde
    el propio stream (MEF-ADR-0004 intacto). Sigue **rechazado** el "evento gordo" que todas las plantillas
    escuchan en el `Apply`.
  - **Desorden del bus**: la plantilla ignora una version menor o igual a la de su copia.
  - **Borrados**: `TurnoRetirado` llega como sincronizacion con `Retirado`; la plantilla queda **incompleta**,
    sin bloqueo ni cascada (se conserva).
  - **Backfill**: no hay; las plantillas existentes son datos de prueba y se purgan en el mismo despliegue que
    #888 (MEF-ADR-0036).
  - **Drift por el indice eventual**: la reaccion busca las plantillas en `CuadroDeTurnos` (proyeccion
    `Async`); una asignacion aun no proyectada puede quedar fuera. **Autocorreccion**: reasignar el mismo
    `TurnoId` (o reasociar la misma Jornada) es no-op (CA-ADR-0035) **solo si la copia esta al dia**; si la
    version del catalogo es mayor, actualiza la copia. El handler de asignar dia ya carga `CatalogoTurnos`
    (`EvaluarAsignabilidad`): no agrega lecturas. Ventana corta aceptada por el experto.
- Se conserva: retirar un turno referenciado **no se bloquea ni cascadea** (la plantilla queda **incompleta**,
  espejo de *Turno incompleto*: existe, se ve, se edita, y quien la use recibe 409 hasta que se reemplace el
  dia); aplicar una plantilla (#828) sigue programando desde el catalogo por `TurnoId`.

### 3. El descanso es un turno, no una omision

- Cada dia debe tener turno -- de trabajo o de descanso del catalogo (`EsDescanso`). Un dia sin turno deja
  la plantilla **incompleta**. Completa = los N dias del ciclo con turno vigente; es derivada, sin evento propio.
- Consecuencia para la asignacion futura: una plantilla completa produce exactamente N dias de ciclo programados,
  sin agujeros silenciosos (un dia sin plan caeria en `TrabajoSinProgramacion`, otra cosa).

### 4. Ciclo de vida espejo de CA-ADR-0033; el dia es un slot atomico

- Nace vacia (`CrearPlantillaTurnos`, `POST programacion/plantillas-turnos`, paso 1 de MEF-ADR-0043),
  se disena por pasos, es editable siempre, sin estados ni publicacion.
- A diferencia de las franjas del turno (hora `HH:mm` no URL-safe -> `:verbo`), el dia **si** tiene clave
  URL-safe (su numero en el ciclo) y es un slot atomico: `PUT .../{id}/dias/{dia}` reemplaza (paso 2) y
  `DELETE .../dias/{dia}` vacia (paso 3). Corregir = reemplazar, no quitar + agregar.
- **Codigos de exito (enmienda 2026-09-05, CA-ADR-0035)**: `201 Created` + `Location` en el `POST`, `204 No
  Content` en `PUT`/`DELETE` -- la transaccion confirma antes de responder; el 202 heredado del marco era
  impreciso. **Idempotencia**: PUT con el mismo turno, DELETE sobre un dia ya vacio y retirar una plantilla ya
  retirada son no-ops -> exito sin evento (`SinCambios`), nunca 409 (precision 2026-10-06, decision 2: el PUT
  con el mismo turno solo es no-op si la copia esta al dia; si la version del catalogo es mayor, actualiza la
  copia). El 409 queda para conflictos reales (plantilla retirada, dia fuera del ciclo, turno
  retirado/incompleto) y el 404 para recurso inexistente.
- **Solo turnos completos son asignables a un dia** (decision del experto 2026-09-05, espejo de #613): 409
  `TurnoIncompleto`. La verificacion lee el aggregate `CatalogoTurnos` del mismo store (`EvaluarAsignabilidad`),
  no la vista `FichaTurno`: local, sincrono y consistente.
- El numero de dias del ciclo es fijo al crear: cambiarlo = retirar + crear (la plantilla no tiene historia que preservar).
- Nombre unico best-effort contra la vista (espejo de #497: trim, case-insensitive, acentos significativos).
  Retiro (`DELETE .../{id}`) espejo de #500/#501: deja de ser usable, su cuadro se borra, el nombre queda libre.

### 5. Read-side: `CuadroDeTurnos`, N1 con datos propios, nombre + `ToString()` del turno

- Vista `CuadroDeTurnos` ("cuadro de turnos": termino real de salud y vigilancia para la grilla
  dias x turnos; aqui el cuadro de una plantilla, sin personas). Lo que el Programador lee, por dia: turno con
  nombre y `Descripcion` (el `ToString()` del turno), si esta retirado o incompleto -- **no** el objeto
  completo; las franjas siguen siendo `obtener_turno`. A nivel plantilla: `Nombre`, numero de dias, `Completa`.
- **Enmienda 2026-09-05 (opcion B)**: N1 + composicion en la lectura con `FichaTurno` (#624, #625).
  **Superada el 2026-10-06 (#886)**.
- **Enmienda 2026-10-06 (#886): N1 con datos propios.** `CuadroDeTurnos` sigue siendo
  `SingleStreamProjection` (N1) sobre el stream de la plantilla, pero nombre, descripcion, completo y retirado
  de cada dia **llegan en los eventos de la plantilla** (#888, #883), alimentados por la copia sincronizada de
  la decision 2. El GET deja de componer con `FichaTurno` (#882).
- **Ventana eventual**: la descripcion puede quedar vieja unos segundos tras editar el turno, hasta que la
  sincronizacion llegue a la plantilla.
- **Se conserva el rechazo del N2 con grouper custom**: obligaba a la proyeccion a consultar estado externo
  contra la regla de procedencia del Skill `projections`, tenia una carrera de lote y seria el primer grouper
  del BC. Con la copia dentro del stream de la plantilla ya no hace falta. El worker sigue sin referenciar
  Function Apps (CA-ADR-0028).
- **"Ficha" no es un patron de naming.** El experto: "lo usamos para resolver colaboradores y ahora siento
  que todo es Ficha... es como decir 'Vista'". Cada vista se nombra desde el actor que la lee
  (MEF-ADR-0041); las `Ficha*` existentes se quedan.

### 6. Tools MCP: interfaces semanal y rotativa sobre el ciclo; el turno inline se crea en el catalogo

Enmienda 2026-10-27 (#1302; #1288, #1300). Las tools componen sobre el ciclo (MEF-ADR-0047 decision 4):

- **Semanal** = ciclo de **exactamente 7 dias** (dia 1 = lunes). Al aplicarla se alinea al **lunes de la
  semana de `desde`**, de modo que el dia 1 cae en lunes; aplicar con una plantilla cuyo ciclo **no es de 7
  dias** se rechaza.
- **Rotativa** = **bloques consecutivos en orden** (p. ej. 6 dias de un turno, 2 de descanso): el asistente
  arma el ciclo concatenando los bloques y la fecha de inicio da la rotacion.
- La creacion recibe la composicion completa y hace `POST` + N `PUT` bajo el capo. `dias` viaja como **JSON en
  texto** (los parametros de tool son strings planos); los `PUT` van **secuenciales** (mismo stream); se
  resuelven todos los nombres de turno antes del `POST` y no se crea nada si alguno falta. El turno inline se
  **difiere a #651**: un dia descrito inline ("07:00-17:00", sin nombre) se resuelve creando el turno en el
  catalogo con nombre derivado y referenciandolo: la friccion de nombrar se paga en el asistente, no en el
  dominio (el turno inline sin nombre se **rechazo** como concepto: duplicaria la superficie de diseno de
  CA-ADR-0033 o produciria turnos "pobres").

## Alternativas consideradas

- **Nombres**: *Horario* (lo que dice el trabajador; roza con `HorarioResumido`), *Esquema de turnos*,
  *Semana tipo* (muere con N>1), *Malla/Sabana* (nombran la grilla asignada, no el molde), *Patron*
  (= empleador en derecho laboral). El experto eligio **Plantilla de turnos**; "Semanal" se sumo al
  reconocer que los ciclos posicionales quedan fuera.
- **`PlantillaTurnos` generico**: descartado -- renombrar eventos persistidos despues es caro y el
  generico debe quedar como paraguas.
- **Modelo posicional (ciclo de N dias)** unificando semana y rotacion: cubre 4x2 pero pierde el
  vocabulario "lunes/martes" con el que el experto define la plantilla. Diferido: sera otro aggregate.
- **Turno inline sin nombre**: ver decision 6.
- **Evento gordo escuchado en el `Apply` / bloquear el retiro del turno**: ver decision 2 (la copia sincronizada
  por reaccion + comando la reemplaza desde 2026-10-06).
- **N1 con nombre de turno copiado en el evento de plantilla**: bastaba si la vista mostrara solo nombres
  (inmutables: no hay `RenombrarTurno`); el experto quiso el `ToString()`, que si cambia -> N2. Superada el
  2026-09-05 por N1 **sin copia** + composicion en lectura y, el 2026-10-06, por N1 con datos propios
  (decision 5 enmendada).
- **Dia vacio = "sin plan" deliberado**: descartado; descanso es turno, vacio es incompleto.
- **`AgregarSemana`/`QuitarSemana`**: sin caso de uso; retirar + crear.

## Consecuencias

### Positivas
- "Semana Cocina" se crea una vez y se reutiliza; el patron queda nombrado y auditable en el store.
- ControlHoras no cambia: la asignacion futura seguira produciendo un turno por fecha.
- Un solo lugar donde se edita un turno; la plantilla lo copia y se mantiene al dia por sincronizacion
  (consistencia eventual con autocorreccion, decision 2) y puede auditarse a si misma al decidir (#868/#889).
- El GET del cuadro lee un solo documento, sin composicion (#882).
- La puerta a la modalidad ciclica queda abierta sin renombrar nada.

### Negativas
- 4 tipos de evento persistidos y 4 comandos nuevos en Programacion, mas los artefactos de sincronizacion
  (evento privado plano, reaccion, comando y evento por plantilla; #883, #884, #888).
- Verificaciones best-effort entre streams (turno existe/activo, nombre unico) sin atomicidad -- mismo
  perfil que #497.
- La decision del 2026-08-29 queda parcialmente revertida: el glosario debe leerse con esta enmienda.
- Ventana eventual entre editar un turno (o los limites de la Jornada) y que la copia de la plantilla y el
  cuadro se actualicen; acotada por la autocorreccion al reasignar. (La enmienda 2026-09-05 la habia eliminado
  resolviendo la descripcion al leer; la enmienda 2026-10-06 la reintroduce a cambio de la autoauditoria.)
- La reaccion depende del indice eventual `CuadroDeTurnos`: una asignacion aun no proyectada puede quedar
  sin sincronizar hasta que se reasigne.

## Aplicar una plantilla

Enmienda 2026-10-04 (#828). Reescrita 2026-10-27 (#1302) para el ciclo generico.

- **Composicion del asistente.** Aplicar es una composicion: no hay asignacion persistida ni aggregate nuevo, y
  el hecho "tiene la plantilla X" **no** se registra. La tool emite las mismas solicitudes que
  `solicitar_programacion_turno`, una por colaborador y por turno distinto.
- **Fecha de inicio.** Al aplicar se da una fecha de inicio: en ella **cae el dia 1** del ciclo. Puede ser
  **anterior a la ventana** (`desde`); los dias anteriores al inicio **no se programan**. Reusar la misma fecha
  de inicio en una aplicacion posterior **continua la rotacion** en lugar de reiniciarla.
- **Dia del ciclo.** Para una fecha `f` con inicio `i` y un ciclo de `N` dias, el dia del ciclo es
  `((f - i) mod N) + 1`. Depende solo del calendario, no de recortes por vigencia.
- **Paridad de la regla de dias.** Los dias de cada colaborador salen del mismo ejecutor que
  `solicitar_programacion_turno` (ventana cruzada con la vigencia de la vinculacion, sin mirar hoy); la tool
  solo aporta la asignacion fecha -> turno. El recorte por vigencia no reinicia el molde.
- **Cascada de sede (#827).** Sede explicita invalida rechaza todo; sin explicita se usa la sede del
  colaborador; si no hay, se envia `null` con aviso cuando algun turno programado tiene franjas sin sede.
- La interfaz semanal alinea el inicio al lunes (decision 6); el modelo anterior (semana 1 = semana calendario
  que contiene `desde`, avance cada lunes) es el caso particular de un ciclo de 7xS dias con inicio en lunes.

## Retiro de la plantilla semanal

Enmienda 2026-10-27 (#1302; ejecutado en #1289).

- Los tipos de evento de la modalidad semanal (`PlantillaSemanalCreada`, `DiaDePlantillaSemanalAsignado`,
  `DiaDePlantillaSemanalQuitado`, `PlantillaSemanalRetirada` y sus eventos de advertencias y sincronizacion) y
  el aggregate `PlantillaSemanalTurnos` se **borraron sin purga**. Las filas de `mt_events` quedan
  **huerfanas**: ningun lector las carga (no hay aggregate ni proyeccion que las pida) y el daemon de
  proyecciones filtra por los tipos que conoce y **salta los tipos desconocidos** (fuentes de Marten citadas en
  #1289).
- **No se usa SQL contra el store** para limpiarlas (CA-ADR-0040): ningun script borra de `mt_events` ni
  `mt_streams`.
- **MEF-ADR-0036 no aplica.** Su protocolo (dos despliegues, purga) rige **mover o renombrar un tipo que se
  sigue leyendo**: hay que mantener un alias legible mientras existan eventos viejos. Aqui el tipo se borra y
  nadie lo lee; no hay lectura que proteger, asi que no hay transicion que coordinar.
- Las plantillas semanales existentes eran datos de prueba; no se migran.

## Referencias

- Issues: #620 (crear), #621 (asignar dia), #622 (quitar dia), #623 (retirar), #624 (vista N1), #625
  (GET + composicion), #626 (nombre unico), #627-#628 (tools de Comandos), #629 (tools de Consultas), #651
  (turno inline), #640 (correccion de codigos HTTP de los endpoints existentes); #886 (enmienda de copia
  sincronizada); #1280-#1300 (plantilla generica), #1289 (retiro de la semanal), #1302 (esta enmienda), #887 (VO `Turno`), #888, #883, #884, #867, #868, #889, #882; harness#849, harness#850.
- MEF-ADR-0004, MEF-ADR-0011, MEF-ADR-0012, MEF-ADR-0018, MEF-ADR-0034, MEF-ADR-0035, MEF-ADR-0036,
  MEF-ADR-0041, MEF-ADR-0042, MEF-ADR-0043, MEF-ADR-0046 (citado para declarar que no aplica, decision 2),
  MEF-ADR-0047, MEF-ADR-0048;
  CA-ADR-0028, CA-ADR-0029, CA-ADR-0030, CA-ADR-0031, CA-ADR-0033, CA-ADR-0035.
- Glosario: Plantilla de turnos, Plantilla semanal de turnos (historica), Cuadro semanal de turnos (historica), Turno (enmienda),
  Ventana de trabajo, Programador de turnos.
- Skill `projections`, `modelos-marten.md` (regla de procedencia: un metodo de proyeccion no lee estado externo;
  regla 3 sobre N2 con grouper, descartada aqui) y `read-apis.md` (DTO de respuesta por composicion).

## Control de cambios

- 2026-09-04: creado (sesion planner; creacion de #620-#629).
- 2026-09-05: enmendado (sesion planner; refinamiento de #620-#629 a `estado:listo`). Decision 1: tope 6 semanas,
  `DiaSemana` ISO 1..7. Decision 4: 201/204 (CA-ADR-0035), no-ops idempotentes sin 409, solo turnos completos
  asignables. Decision 5: N2 con grouper -> N1 + composicion en lectura (opcion B), con las hipotesis del
  write-side descartadas. Decision 6: `dias` JSON, PUT secuenciales, turno inline diferido a #651. Nace #640
  (inventario de codigos) y CA-ADR-0035.
- 2026-10-04: enmendado (#828). Se agrega "Aplicar una plantilla" y se actualiza el parrafo que dejaba asignar fuera de alcance.
- 2026-10-06: enmendado (#886, sesiones del planner). Decision 2: referencia viva sin copia -> `TurnoId` +
  copia de valor sincronizada (turno #887 y limites de Jornada #867) por reaccion + comando; desorden por
  version, retiro como sincronizacion, sin backfill (purga con #888), autocorreccion al reasignar. Se corrige la
  cita a MEF-ADR-0046 (no aplica). Decision 5: N1 con datos propios, sin composicion con `FichaTurno` en el GET
  (#882, #888, #883); el rechazo del N2 con grouper se conserva. Consecuencias: consistencia eventual con
  autocorreccion. Habilita la autoauditoria (#868/#889) y los artefactos #883/#884.
- 2026-10-27: enmendado (#1302; sesion planner 2026-10-26 y refinamiento de #1289). Titulo y decision 1: de molde
  semanal de 1..6 semanas a ciclo de dias 1..N (N <= 42) con `PlantillaTurnos` en `programacion/plantillas-turnos`.
  Decisiones 2 a 5 conservadas con la generica como sujeto (`CuadroDeTurnos` en la 5). "Aplicar una plantilla":
  fecha de inicio y dia del ciclo `((fecha - inicio) mod N) + 1`. Decision 6: interfaces semanal (7 dias, alineada
  al lunes) y rotativa (bloques). Nueva seccion "Retiro de la plantilla semanal": borrado sin purga, sin SQL
  (CA-ADR-0040), MEF-ADR-0036 no aplica.
