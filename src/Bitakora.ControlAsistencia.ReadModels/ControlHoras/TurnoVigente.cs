namespace Bitakora.ControlAsistencia.ReadModels.ControlHoras;

/// <summary>
/// Read model del turno que rige a un colaborador en una fecha (issue #328), disenado desde la
/// necesidad de lectura (panorama del programador, consulta del trabajador, consulta puntual del
/// aprobador) y no desde el evento disponible. Reemplazo del read model anterior del issue #289,
/// que la contraccion del issue #323 ya retiro.
///
/// Record plano SIN partial (MEF-ADR-0035, skills/projections/modelos-marten.md): el comportamiento
/// de proyeccion vive en la clase companion TurnoVigenteProjection, en el worker
/// (Bitakora.ControlAsistencia.Projections). Este tipo vive en ReadModels (MEF-ADR-0034 seccion 5),
/// la cuarta isla del repo: cero referencias de proyecto (issue #323, ver el .csproj) --
/// Bloque/TipoBloque son propios, sin relacion de tipo con ControlHoras.DomainEvents ni con los
/// ensamblados de bus.
///
/// Divergencia documentada frente al naming canonico del Skill (naming.md: "{Concepto}View"): este
/// read model NO lleva sufijo "View" -- decision del repo, extension de la proscripcion de sufijos
/// de infraestructura del issue #317 CA-2 al read-side (ver issue #328, "ADRs aplicables").
///
/// Id es el stream key que compone ControlDiarioAggregateRoot.ComputarStreamId:
/// "cd:{CodigoColaborador}:{Fecha:yyyyMMdd}" -- nunca un Guid (Events.StreamIdentity = AsString).
/// Excluye deliberadamente la trazabilidad interna hacia la solicitud de programacion y la identificacion
/// completa del colaborador (ambas las cargaba el read model del issue #289, que ningun cliente de
/// calendario consumia): solo CodigoColaborador (lookup/resourceId) y NombreCompleto (un solo campo,
/// concatenado por la proyeccion desde ColaboradorProgramado.Nombres +
/// ColaboradorProgramado.Apellidos -- unico lugar del sistema donde se hace, issue #328
/// "Investigacion del planner").
/// </summary>
public sealed record TurnoVigente(
    string Id,
    string CodigoColaborador,
    string NombreCompleto,
    DateOnly Fecha,
    string NombreTurno,
    string HorarioResumido,
    IReadOnlyList<Bloque> Bloques);
