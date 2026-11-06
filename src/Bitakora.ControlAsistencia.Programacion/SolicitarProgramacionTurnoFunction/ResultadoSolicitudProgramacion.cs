namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;

public record ResultadoSolicitudProgramacion(
    IReadOnlyList<FechaRespetada> FechasRespetadas,
    IReadOnlyList<DateOnly> FechasEnBorrador);

public record FechaRespetada(DateOnly Fecha, string Motivo);
