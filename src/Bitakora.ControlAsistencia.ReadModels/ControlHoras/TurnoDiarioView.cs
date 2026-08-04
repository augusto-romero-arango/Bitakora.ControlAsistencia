using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.PublicEvents.Empleados;

namespace Bitakora.ControlAsistencia.ReadModels.ControlHoras;

/// <summary>
/// Read model del turno diario vigente para un empleado en una fecha (issue #289). Es la primera
/// proyeccion concreta del BC: "Turno Diario" (context: ControlHoras) es el turno que rige una
/// fecha concreta, en contraste con "Turno" (context: Programacion), la plantilla del catalogo.
///
/// Record plano SIN partial (MEF-ADR-0035, skills/projections/modelos-marten.md): el comportamiento
/// de proyeccion vive en la clase companion TurnoDiarioProjection, en el worker
/// (Bitakora.ControlAsistencia.Projections). Este tipo vive en ReadModels (MEF-ADR-0034 seccion 5)
/// y no referencia Marten ni transitivamente -- Empleado y DetalleTurno son DTOs planos
/// reutilizados de PublicEvents/PrivateEvents, sin invariantes propios.
///
/// Id es el stream key que compone ControlDiarioAggregateRoot.ComputarStreamId:
/// "{EmpleadoId}:{Fecha:yyyy-MM-dd}" -- nunca un Guid (Events.StreamIdentity = AsString).
/// UltimaSolicitudId es la unica trazabilidad hacia la SolicitudProgramacion que asigno el turno
/// vigente (el evento no conserva el turnoId del catalogo, ver "limite conocido" del issue #289).
/// </summary>
public sealed record TurnoDiarioView(
    string Id,
    InformacionEmpleado Empleado,
    DateOnly Fecha,
    DetalleTurno DetalleTurno,
    Guid UltimaSolicitudId);
