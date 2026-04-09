using Bitakora.ControlAsistencia.Contracts.Empleados.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;

namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;

public record SolicitarProgramacionTurno(
    Guid Id,
    Guid TurnoId,
    InformacionEmpleado Empleado,
    List<DateOnly> Fechas);
