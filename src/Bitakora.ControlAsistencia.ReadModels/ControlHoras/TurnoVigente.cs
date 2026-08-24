namespace Bitakora.ControlAsistencia.ReadModels.ControlHoras;

/// <summary>
/// Read model del turno que rige a un colaborador en una fecha, disenado desde la necesidad de
/// lectura (panorama del programador, consulta del trabajador, consulta puntual del aprobador) y no
/// desde la forma del evento disponible (MEF-ADR-0041).
///
/// Record plano SIN partial (MEF-ADR-0035): el comportamiento de proyeccion vive en la clase
/// companion TurnoVigenteProjection, en el worker. Este tipo vive en ReadModels, la cuarta isla del
/// repo -- cero referencias de proyecto (ver el .csproj): Bloque/TipoBloque son propios, sin
/// relacion de tipo con ControlHoras.DomainEvents ni con los ensamblados de bus.
///
/// Divergencia deliberada frente al naming canonico del Skill (naming.md: "{Concepto}View"): este
/// read model NO lleva sufijo de implementacion -- decision del repo, extension al read-side de la
/// proscripcion de sufijos de infraestructura.
///
/// Id es el stream key que compone ControlDiarioAggregateRoot.ComputarStreamId
/// ("cd:{CodigoColaborador}:{Fecha:yyyyMMdd}"), nunca un Guid (Events.StreamIdentity = AsString).
/// Excluye a proposito la trazabilidad hacia la solicitud de programacion y la identificacion
/// completa del colaborador: ningun cliente de calendario las consume. NombreCompleto llega ya
/// concatenado en el payload del evento -- el worker no lo compone.
/// </summary>
public sealed record TurnoVigente(
    string Id,
    string CodigoColaborador,
    string NombreCompleto,
    DateOnly Fecha,
    string NombreTurno,
    string HorarioResumido,
    IReadOnlyList<Bloque> Bloques);
