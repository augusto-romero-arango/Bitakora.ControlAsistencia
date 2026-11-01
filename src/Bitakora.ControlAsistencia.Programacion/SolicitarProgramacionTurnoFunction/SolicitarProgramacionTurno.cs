using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;

public record SolicitarProgramacionTurno(
    Guid Id,
    Guid TurnoId,
    ColaboradorSolicitado Colaborador,
    List<DateOnly> Fechas,
    SedeProgramada? Sede = null,
    Guid? JornadaId = null);
