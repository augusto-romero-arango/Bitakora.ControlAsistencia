using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;

// Issue #331: Sede es opcional (null = sin sede asignada). El cliente la resuelve (sede natural
// del colaborador por default, o la que el Programador indique) -- el servidor NUNCA consulta el
// maestro de sedes (#330). Reutiliza SedeProgramada (Programacion.DomainEvents).
// Issue #436: Colaborador pasa a ColaboradorSolicitado, DTO propio de este feature folder con la
// terna de identidad, en vez del quinteto de InformacionColaborador (PublicEvents). Cambian las
// claves del body HTTP; verbo y ruta quedan intactos, asi que el test de precedencia de
// MEF-ADR-0043 no se re-ejecuta (precedente #401).
public record SolicitarProgramacionTurno(
    Guid Id,
    Guid TurnoId,
    ColaboradorSolicitado Colaborador,
    List<DateOnly> Fechas,
    SedeProgramada? Sede = null);
