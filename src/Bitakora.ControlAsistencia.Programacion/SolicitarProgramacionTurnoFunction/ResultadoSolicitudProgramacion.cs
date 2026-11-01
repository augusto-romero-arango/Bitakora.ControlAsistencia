namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;

public record ResultadoSolicitudProgramacion(IReadOnlyList<FechaRespetada> FechasRespetadas);

public record FechaRespetada(DateOnly Fecha, string Motivo);
