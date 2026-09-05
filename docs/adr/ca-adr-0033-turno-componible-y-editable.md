# CA-ADR-0033: Turno componible y editable

## Estado

Aceptado (sesion de planeacion 2026-09-04; implementacion en #598-#613).

## Contexto

Hasta hoy un turno del catalogo se creaba **de una vez** con `CrearTurno` (POST `programacion/turnos`)
y solo admitia dos operaciones posteriores: solicitar programacion y retirar. Tres decisiones previas
sostenian ese modelo: #423 fijo que "cero franjas = descanso" (biunivoco por construccion) y que la
marca `EsDescanso` vive **solo** en el DTO de frontera; #500/#501 decidieron que "modificar turno no se
construye" (modificar = retirar + crear). El glosario y `FichaTurnoProjection` derivaban el descanso
del conteo de franjas.

Dos fuerzas rompen ese modelo:

1. **Crear turnos debe ser nativamente agentico.** El Programador de turnos conversa con un asistente
   MCP: "crea el Nocturno" -> "agregale una franja de 22 a 6" -> "con descanso a las 2" -> "programalo".
   Un formulario atomico obliga al asistente a acumular todo el diseno antes del primer POST y a
   recrear el turno completo ante cualquier correccion.
2. **Defectos verificados en el dominio real** (2026-09-04): `SubFranja.Crear(23:30, 00:30)` construye
   una franja de -1380 min que `FranjaOrdinaria.Crear` acepta como descanso (la contencion pasa por
   casualidad); un descanso de madrugada `02:00-02:30` en `22:00-06:00+1` se **rechaza** porque nadie
   infiere sus offsets; `CrearTurno.Franja` declara descansos/extras como `ValueTuple` y System.Text.Json
   no serializa sus campos, asi que el endpoint **nunca pudo recibir hijas** (`00:00-00:00` -> 400).

Verificado tambien contra dev: 433 fichas vigentes, todas `[TEST]` (41 descansos de smoke). No hay
datos reales que preservar.

## Decision

### 1. El turno es componible y editable siempre

- Un turno **nace vacio** y se le agregan franjas por pasos (**Diseno de turno**, glosario). La via
  atomica `CrearTurno` con franjas se conserva.
- No hay estados ni publicacion (sin "borrador/publicado"): todo turno es editable en cualquier
  momento. Las solicitudes ya hechas no cambian: copian su snapshot (`ObtenerDetalle()`).
- Un turno es **programable cuando esta completo**: `>= 1` franja ordinaria, o declarado descanso.
  `SolicitarProgramacionTurno` rechaza con 409 (`TurnoIncompleto`) lo demas (#613).

### 2. `EsDescanso` entra al evento y al aggregate

Muere la biunivocidad "cero franjas = descanso": un turno vacio puede ser descanso o **Turno
incompleto**. `TurnoCreado` gana `bool EsDescanso` (plano); `CatalogoTurnos` gana `_esDescanso` y
`EstaCompleto()`. Los streams legados sin la clave deserializan `false` (solo residuo `[TEST]`): sin
`bool?`, sin `TurnoCreadoV2` (MEF-ADR-0036 Alt 2: V2 es para contrato de bus) y sin purga como CA
(MEF-ADR-0036 seccion 5). Revierte parcialmente #423 y la premisa de #500/#501.

### 3. Comandos de diseno: paso 4 de MEF-ADR-0043, franja direccionable por hora de inicio

`POST programacion/turnos/{id}:agregar-franja | :quitar-franja | :agregar-subfranja |
:quitar-subfranja | :asignar-sede-franja`. La franja se identifica por su **hora de inicio** (unica
por la invariante de no-solape); la hija por franja + hora de inicio. `HH:mm` contiene `:` (fuera
del charset URL-safe, MEF-ADR-0043 seccion 1.1), por eso no hay `DELETE`/`PUT` por sub-recurso.
**Corregir = quitar + agregar** (sin `Mover*`, Rule of Three MEF-ADR-0018). Quitar una franja
arrastra sus descansos, extras y sede en un solo evento. Descanso y extra comparten un comando con
discriminador de frontera `tipo` (string validado; el evento distingue por nombre).

### 4. Los eventos de diseno llevan la franja completa

`FranjaAgregada`, `DescansoAgregado`, `ExtraAgregado`, `DescansoQuitado`, `ExtraQuitado`,
`SedeDeFranjaAsignada`, `SedeDeFranjaRetirada` llevan `(TurnoId, Franja)` con la franja contenedora
**resultante**; `FranjaQuitada` lleva la franja **removida** (su resultado es una ausencia). Asi
`Apply` -- en el aggregate y en la proyeccion -- reemplaza o quita por hora de inicio **sin llamar
ningun factory** (MEF-ADR-0004: `Apply` no lanza, aunque las invariantes se endurezcan despues). Todos
se registran en `IdentidadEventosProgramacion.TiposPersistidos` (CA-ADR-0029 decision 6): 4 -> 12.

### 5. Invariantes de duracion y offsets

- Toda franja (ordinaria o hija) tiene **duracion positiva** (`DuracionNoPositiva` reemplaza a
  `InicioYFinIguales`). La ordinaria dura **como maximo 24 h inclusive** (`DuracionExcedeUnDia` para
  `> 1440 min`): la jornada maxima es 24 h y existen esquemas 24x24 (decision del experto).
- Los offsets de las **hijas no son entrada**: se infieren relativos a la franja contenedora
  (`inicio < inicioFranja => +1`; `fin < inicioHija => +1 mas`). Con la franja acotada a 24 h cada
  `HH:mm` tiene un unico dia posible, asi que un offset explicito solo podria coincidir o violar
  contencion. `SubFranja` sigue **persistiendo** sus offsets; ficha y ControlHoras los reciben igual.
  La inferencia vive en `FranjaOrdinaria` (`ConDescanso`/`ConExtra`), que la usan la via atomica y
  `AgregarSubFranja`.
- La ordinaria conserva `diaOffsetFin` opcional explicito: unico caso que lo necesita es la franja de
  24 h exactas (`08:00 -> 08:00+1`).

### 6. Contrato HTTP de `CrearTurno`

`ordinarias` opcional (ausente = vacio); ordinaria `{ inicio, fin, diaOffsetFin?, descansos?, extras?,
sede? }`; hija `{ inicio, fin }` (record, no tupla). Verbo y ruta no cambian (paso 1).

### 7. Read-side y tools

- `FichaTurno` sigue los 9 eventos de diseno, lee `EsDescanso` del evento y gana `Completo` (derivado
  de lectura, MEF-ADR-0041). Franjas ordenadas por hora de inicio; incompleto = "Sin franjas". Sin
  filtro server-side: el catalogo es acotado y el cliente filtra (MEF-ADR-0042 seccion 1); el listado
  incluye los incompletos porque el resolutor por nombre los necesita.
- Tools MCP de Comandos: referencia al turno **por nombre** (resolutor compartido), horas `HH:mm` sin
  offsets (`inicio == fin` en `agregar_franja` significa 24 h), eco = lo enviado + nota de visibilidad
  eventual (nunca se relee la ficha tras el POST). Consultas: `enConstruccion` solo cuando aplica;
  notacion unificada `HH:mm[+N]-HH:mm[+N]` en ambos servidores (islas: se replica el formato).

## Alternativas consideradas

- **`bool? EsDescanso` con derivacion en `Apply`** o **`TurnoCreadoV2`**: descartadas -- protegen residuo
  de smoke al costo de un nullable perpetuo o de duplicar serializacion/identidad/proyeccion.
- **Offsets explicitos opcionales en las hijas** ("decision C" de la sesion anterior): descartada tras
  el analisis de unicidad; el experto confirmo "la inferencia se hace para el DTO de entrada, adentro
  todo queda con offset".
- **`DELETE .../franjas/{hora}` / `PUT .../franjas/{hora}/sede`**: descartadas -- exigen una clave de
  ruta que el dominio no tiene (MEF-ADR-0043 seccion 1.1).
- **`MoverFranja`/`CorregirFranja`**: sin tercer caso de uso; quitar + agregar lo cubre.
- **Payload de hijas = solo la sub-franja**: obliga a `Apply` a reconstruir via factory.
- **Filtro `?completo=` y parametro `solo_programables`**: sin necesidad que los justifique.
- **Enum en DTO HTTP / `inputSchema`**: sin precedente en el repo y con gate de binding; string validado.
- **Tope `< 24 h`**: descartado por el experto (24x24).

## Consecuencias

### Positivas
- El diseno conversacional es posible sin formulario y sin recrear el turno ante cada correccion.
- Se cierran tres defectos reales (duracion negativa, madrugada rechazada, hijas no deserializables).
- `Apply` sin factories en aggregate y proyeccion: rehidratacion estable ante invariantes futuras.
- Un solo lugar para la inferencia de offsets, reutilizado por las dos vias.

### Negativas
- 12 tipos de evento persistidos en Programacion (antes 4) y 5 comandos nuevos: mas superficie que
  mantener en identidad, serializacion y pins de tools.
- Los eventos de diseno repiten la franja completa: payload mayor que el delta; la auditoria del
  "que cambio" exige comparar con el estado previo.
- Los 41 descansos `[TEST]` de dev se releen como incompletos en el aggregate (y en la ficha si hay
  rebuild). Aceptado: residuo de smoke.
- Ventana en dev entre #599 y #607 en la que un turno vacio se ve como "Descanso" en la ficha.

## Referencias

- Issues: #598 (invariantes), #599 (evento/aggregate/DTO), #613 (guarda de solicitud), #600
  (inferencia), #601 (DTO), #602-#606 (comandos de diseno), #607 (ficha), #608-#611 (tools de
  Comandos), #612 (tools de Consultas). Reemplazan/enmiendan: #423, #500, #501.
- MEF-ADR-0004, MEF-ADR-0012, MEF-ADR-0018, MEF-ADR-0034, MEF-ADR-0036, MEF-ADR-0041, MEF-ADR-0042,
  MEF-ADR-0043, MEF-ADR-0047, MEF-ADR-0048; CA-ADR-0029, CA-ADR-0030.
- Glosario: Turno, Turno incompleto, Diseno de turno, Franja Ordinaria, Franja de Descanso, Franja
  de Extra, Offset de dia, FichaTurno.

## Control de cambios

- 2026-09-04: creado (sesion planner; refinamiento de #598-#612, creacion de #613).
