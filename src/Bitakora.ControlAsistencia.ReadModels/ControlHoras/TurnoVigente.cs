namespace Bitakora.ControlAsistencia.ReadModels.ControlHoras;

/// <summary>
/// Read model del turno que rige a un empleado en una fecha (issue #328) -- reemplazo de
/// <c>TurnoDiarioView</c> (issue #289), disenado desde la necesidad de lectura (panorama del
/// programador, consulta del trabajador, consulta puntual del aprobador) y no desde el evento
/// disponible. Convive con TurnoDiarioView hasta que la contraccion (#323) retire el read model
/// viejo -- nombres distintos, sin colision.
///
/// Record plano SIN partial (MEF-ADR-0035, skills/projections/modelos-marten.md): el comportamiento
/// de proyeccion vive en la clase companion TurnoVigenteProjection, en el worker
/// (Bitakora.ControlAsistencia.Projections). Este tipo vive en ReadModels (MEF-ADR-0034 seccion 5)
/// y no gana ninguna referencia de proyecto nueva con este issue -- Bloque/TipoBloque son propios,
/// sin relacion de tipo con ControlHoras.DomainEvents.
///
/// Divergencia documentada frente al naming canonico del Skill (naming.md: "{Concepto}View"): este
/// read model NO lleva sufijo "View" -- decision del repo, extension de la proscripcion de sufijos
/// de infraestructura del issue #317 CA-2 al read-side (ver issue #328, "ADRs aplicables").
///
/// Id es el stream key que compone ControlDiarioAggregateRoot.ComputarStreamId:
/// "{EmpleadoId}:{Fecha:yyyy-MM-dd}" -- nunca un Guid (Events.StreamIdentity = AsString). Excluye
/// deliberadamente UltimaSolicitudId (trazabilidad interna que TurnoDiarioView si expone) y la
/// identificacion completa del empleado: solo EmpleadoId (lookup/resourceId) y NombreCompleto (un
/// solo campo, concatenado por la proyeccion desde Empleado.Nombres + Empleado.Apellidos -- unico
/// lugar del sistema donde se hace, issue #328 "Investigacion del planner").
/// </summary>
public sealed record TurnoVigente(
    string Id,
    string EmpleadoId,
    string NombreCompleto,
    DateOnly Fecha,
    string NombreTurno,
    string HorarioResumido,
    IReadOnlyList<Bloque> Bloques);
