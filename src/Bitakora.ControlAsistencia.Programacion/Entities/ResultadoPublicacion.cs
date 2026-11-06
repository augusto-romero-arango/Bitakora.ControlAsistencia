using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

internal sealed record ResultadoPublicacion(IReadOnlyList<LotePublicado> Lotes);

internal sealed record LotePublicado(
    Guid SolicitudId,
    ColaboradorProgramado Colaborador,
    IReadOnlyList<DateOnly> Fechas,
    TurnoProgramado DetalleTurno,
    SedeProgramada? Sede,
    JornadaProgramada Jornada);
