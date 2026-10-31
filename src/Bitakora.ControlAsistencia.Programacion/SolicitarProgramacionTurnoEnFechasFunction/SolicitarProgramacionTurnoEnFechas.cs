using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;

namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoEnFechasFunction;

public record SolicitarProgramacionTurnoEnFechas(
    Guid Id,
    Guid TurnoId,
    ColaboradorSolicitado Colaborador,
    List<DateOnly> Fechas,
    SedeProgramada? Sede = null,
    Guid? JornadaId = null);
