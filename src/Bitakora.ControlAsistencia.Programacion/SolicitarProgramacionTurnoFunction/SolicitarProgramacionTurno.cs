using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.PublicEvents.Colaboradores;

namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;

// Issue #331: Sede es opcional (null = sin sede asignada). El cliente la resuelve (sede natural
// del colaborador por default, o la que el Programador indique) -- el servidor NUNCA consulta el
// maestro de sedes (#330). Reutiliza SedeProgramada (Programacion.DomainEvents), mismo precedente
// que el colaborador reutilizando InformacionColaborador (PublicEvents).
// Issue #340: el tipo paso de InformacionEmpleado a InformacionColaborador (termino proscrito por
// #330), sin tocar el contrato HTTP.
// Issue #401: el parametro posicional paso de Empleado a Colaborador -- aqui SI cambia la clave del
// body HTTP (POST programacion/solicitudes). Verbo y ruta quedan intactos: ya son conformes y el
// test de precedencia de MEF-ADR-0043 no se re-ejecuta por un cambio de claves del body.
public record SolicitarProgramacionTurno(
    Guid Id,
    Guid TurnoId,
    InformacionColaborador Colaborador,
    List<DateOnly> Fechas,
    SedeProgramada? Sede = null);
