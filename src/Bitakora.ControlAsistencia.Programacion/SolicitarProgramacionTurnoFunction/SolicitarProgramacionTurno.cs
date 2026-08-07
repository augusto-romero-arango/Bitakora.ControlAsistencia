using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.PublicEvents.Empleados;

namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;

// Issue #331: Sede es opcional (null = sin sede asignada). El cliente la resuelve (sede natural
// del empleado por default, o la que el Programador indique) -- el servidor NUNCA consulta el
// maestro de sedes (#330). Reutiliza SedeProgramada (Programacion.DomainEvents), mismo precedente
// que Empleado reutilizando InformacionEmpleado (PublicEvents).
public record SolicitarProgramacionTurno(
    Guid Id,
    Guid TurnoId,
    InformacionEmpleado Empleado,
    List<DateOnly> Fechas,
    SedeProgramada? Sede = null);
