using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.PublicEvents.Colaboradores;

namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;

// Issue #331: Sede es opcional (null = sin sede asignada). El cliente la resuelve (sede natural
// del colaborador por default, o la que el Programador indique) -- el servidor NUNCA consulta el
// maestro de sedes (#330). Reutiliza SedeProgramada (Programacion.DomainEvents), mismo precedente
// que el colaborador reutilizando InformacionColaborador (PublicEvents).
// Issue #340: el tipo paso de InformacionEmpleado a InformacionColaborador (termino proscrito por
// #330). El nombre del parametro posicional -- la clave del body HTTP -- no cambia: el contrato
// HTTP queda intacto (lo renombra #401).
public record SolicitarProgramacionTurno(
    Guid Id,
    Guid TurnoId,
    InformacionColaborador Empleado,
    List<DateOnly> Fechas,
    SedeProgramada? Sede = null);
