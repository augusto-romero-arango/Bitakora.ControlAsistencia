# CA-ADR-0034: Plantilla semanal de turnos

## Estado

Aceptado (sesion de planeacion 2026-09-04; implementacion en #620-#629).

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

Fuera de esta decision queda **asignar** una plantilla a un colaborador (acotada o abierta, alineacion al
lunes, si el hecho "tiene la plantilla X" se registra). El experto lo saco explicitamente del foco: esta
sesion es de **construccion** de plantillas.

## Decision

### 1. Plantilla semanal de turnos: molde de 1..N semanas, lunes a domingo, con referencias al catalogo

- Aggregate `PlantillaSemanalTurnos` (stream por plantilla; Id Guid del cliente, nombre, numero de semanas
  fijado al crear). Cada dia se identifica por `(semana, diaSemana)` y referencia un turno del catalogo por
  `TurnoId`. Con N=1 la semana sobra en la conversacion pero no en el modelo: S1+S2 queda cubierto sin
  tercer concepto.
- **Modalidad semanal, explicita en el nombre.** Los ciclos posicionales (4x2, 2x2, 6x1 con descanso que
  corre) no calzan en una semana y serian **otro aggregate** con dias por posicion. Por eso el aggregate,
  los eventos y la ruta llevan "Semanal": el alias de un evento persistido es el nombre simple de la clase
  y una futura `PlantillaCiclica` viviria en el mismo store (CA-ADR-0029 #6, MEF-ADR-0036). El nombre
  generico **Plantilla de turnos** queda libre como termino paraguas del glosario (anti-squatting, mismo
  criterio que `SedeProgramada`/`ColaboradorProgramado`).
- Eventos (`Programacion.DomainEvents`, registrados en `IdentidadEventosProgramacion`):
  `PlantillaSemanalCreada`, `DiaDePlantillaSemanalAsignado`, `DiaDePlantillaSemanalQuitado`,
  `PlantillaSemanalRetirada`. Ninguno cruza bus.

### 2. Referencia viva, no snapshot; el snapshot ocurre al asignar

- La plantilla guarda `TurnoId`, nunca una copia del turno. Editar un turno (CA-ADR-0033) se refleja solo
  en las asignaciones **futuras** de las plantillas que lo referencian; las ya hechas conservan su snapshot
  en la solicitud, como hoy.
- **Rechazado**: snapshot dentro de la plantilla sincronizado con un "evento gordo" que "todas las plantillas
  escuchan en el Apply". `Apply()` solo rehidrata desde el propio stream (MEF-ADR-0004); lo propuesto seria
  evento privado + reaccion + indice `TurnoId -> PlantillaIds` + comando + evento por plantilla, y otro tanto
  para `TurnoRetirado`: una **replica local con sincronizacion**, ultimo recurso segun MEF-ADR-0046 y sin el
  criterio que la justifica (ningun caso de uso necesita el turno en la misma transaccion; leerlo del mismo
  store al asignar es local, sincrono y consistente).
- Retirar un turno referenciado **no se bloquea ni cascadea**: la plantilla queda **incompleta** (espejo de
  *Turno incompleto*): existe, se ve, se edita, y quien la use recibe 409 hasta que se reemplace el dia.

### 3. El descanso es un turno, no una omision

- Cada dia debe tener turno -- de trabajo o de descanso del catalogo (`EsDescanso`). Un dia sin turno deja
  la plantilla **incompleta**. Completa = los 7xN dias con turno vigente; es derivada, sin evento propio.
- Consecuencia para la asignacion futura: una plantilla completa produce exactamente 7xN dias programados,
  sin agujeros silenciosos (un dia sin plan caeria en `TrabajoSinProgramacion`, otra cosa).

### 4. Ciclo de vida espejo de CA-ADR-0033; el dia es un slot atomico

- Nace vacia (`CrearPlantillaSemanal`, `POST programacion/plantillas-semanales`, paso 1 de MEF-ADR-0043),
  se disena por pasos, es editable siempre, sin estados ni publicacion.
- A diferencia de las franjas del turno (hora `HH:mm` no URL-safe -> `:verbo`), el dia **si** tiene clave
  URL-safe `(semana, dia)` y es un slot atomico: `PUT .../{id}/dias/{semana}/{dia}` reemplaza (paso 2) y
  `DELETE .../dias/{semana}/{dia}` vacia (paso 3). Corregir = reemplazar, no quitar + agregar.
- `Semanas` es fijo al crear: cambiarlo = retirar + crear (la plantilla no tiene historia que preservar).
- Nombre unico best-effort contra la vista (espejo de #497: trim, case-insensitive, acentos significativos).
  Retiro (`DELETE .../{id}`) espejo de #500/#501: deja de ser usable, su cuadro se borra, el nombre queda libre.

### 5. Read-side: `CuadroSemanalTurnos`, N2 con grouper, nombre + `ToString()` del turno

- Vista `CuadroSemanalTurnos` ("cuadro de turnos": termino real de salud y vigilancia para la grilla
  dias x turnos; aqui el cuadro de una plantilla, sin personas). Por dia lleva `TurnoId`, `NombreTurno` y
  `Descripcion` (el `ToString()` del turno, lo que `FichaTurno` ya expone) -- **no** el objeto completo;
  las franjas siguen siendo `obtener_turno`. A nivel plantilla: `Nombre`, `Semanas`, `Completa`.
- Como `Descripcion` cambia con el diseno del turno, la vista es **N2 (`MultiStreamProjection`) con
  grouper custom** (regla 3 de `modelos-marten.md`): los eventos de plantilla se rutean por `PlantillaId`;
  los de diseno de turno y `TurnoRetirado` traen `TurnoId` y el grouper consulta la propia vista para
  abrirlos en las plantillas que lo contienen. `NombreTurno`/`Descripcion` se leen de `FichaTurno` (mismo
  store), nunca de `CatalogoTurnos.ToString()` (el worker no referencia Function Apps, CA-ADR-0028).
- Copiar en el read-side es legitimo: la vista es derivada y reconstruible; copiar en el write-side (2) no.
- **"Ficha" no es un patron de naming.** El experto: "lo usamos para resolver colaboradores y ahora siento
  que todo es Ficha... es como decir 'Vista'". Cada vista se nombra desde el actor que la lee
  (MEF-ADR-0041); las `Ficha*` existentes se quedan.

### 6. Tools MCP: composicion consolidada; el turno inline se crea en el catalogo

- `crear_plantilla_semanal` recibe la composicion completa y hace `POST` + N `PUT` bajo el capo
  (MEF-ADR-0047 decision 4). Un dia descrito inline ("07:00-17:00", sin nombre) se resuelve creando el
  turno en el catalogo con nombre derivado y referenciandolo: la friccion de nombrar se paga en el
  asistente, no en el dominio (el turno inline sin nombre se **rechazo** como concepto: duplicaria la
  superficie de diseno de CA-ADR-0033 o produciria turnos "pobres").

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
- **Snapshot + evento gordo / bloquear el retiro del turno**: ver decision 2.
- **N1 con nombre de turno copiado en el evento de plantilla**: bastaba si la vista mostrara solo nombres
  (inmutables: no hay `RenombrarTurno`); el experto quiso el `ToString()`, que si cambia -> N2.
- **Dia vacio = "sin plan" deliberado**: descartado; descanso es turno, vacio es incompleto.
- **`AgregarSemana`/`QuitarSemana`**: sin caso de uso; retirar + crear.

## Consecuencias

### Positivas
- "Semana Cocina" se crea una vez y se reutiliza; el patron queda nombrado y auditable en el store.
- ControlHoras no cambia: la asignacion futura seguira produciendo un turno por fecha.
- Un solo lugar donde vive y se edita un turno; la plantilla nunca drifta respecto al catalogo.
- La puerta a la modalidad ciclica queda abierta sin renombrar nada.

### Negativas
- 4 tipos de evento persistidos y 4 comandos nuevos en Programacion; una vista N2 con grouper custom
  (variante mas compleja del read-side; API a confirmar en Marten 9.12; indice sobre `Dias[].TurnoId`).
- Verificaciones best-effort entre streams (turno existe/activo, nombre unico) sin atomicidad -- mismo
  perfil que #497.
- La decision del 2026-08-29 queda parcialmente revertida: el glosario debe leerse con esta enmienda.
- Ventana entre editar un turno y que el cuadro refresque `Descripcion` (segundos, solo visual).

## Referencias

- Issues: #620 (crear), #621 (asignar dia), #622 (quitar dia), #623 (retirar), #624 (vista N2), #625
  (GET), #626 (nombre unico), #627-#628 (tools de Comandos), #629 (tools de Consultas).
- MEF-ADR-0004, MEF-ADR-0011, MEF-ADR-0012, MEF-ADR-0018, MEF-ADR-0034, MEF-ADR-0035, MEF-ADR-0036,
  MEF-ADR-0041, MEF-ADR-0042, MEF-ADR-0043, MEF-ADR-0046, MEF-ADR-0047, MEF-ADR-0048;
  CA-ADR-0028, CA-ADR-0029, CA-ADR-0030, CA-ADR-0031, CA-ADR-0033.
- Glosario: Plantilla de turnos, Plantilla semanal de turnos, Cuadro semanal de turnos, Turno (enmienda),
  Ventana de trabajo, Programador de turnos.
- Skill `projections`, `modelos-marten.md` regla 3 (N2 con `CustomGrouping`/`IEventSlicer`).

## Control de cambios

- 2026-09-04: creado (sesion planner; creacion de #620-#629).
