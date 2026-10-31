using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoEnFechasFunction;

public record SolicitarProgramacionTurnoEnFechas(
    Guid Id,
    Guid TurnoId,
    ColaboradorSolicitado Colaborador,
    List<DateOnly> Fechas,
    SedeProgramada? Sede = null,
    Guid? JornadaId = null);
