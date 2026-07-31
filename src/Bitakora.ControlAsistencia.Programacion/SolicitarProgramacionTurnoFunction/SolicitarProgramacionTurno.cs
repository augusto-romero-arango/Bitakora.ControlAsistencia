using Bitakora.ControlAsistencia.PublicEvents.Empleados;

namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;

public record SolicitarProgramacionTurno(
    Guid Id,
    Guid TurnoId,
    InformacionEmpleado Empleado,
    List<DateOnly> Fechas);
