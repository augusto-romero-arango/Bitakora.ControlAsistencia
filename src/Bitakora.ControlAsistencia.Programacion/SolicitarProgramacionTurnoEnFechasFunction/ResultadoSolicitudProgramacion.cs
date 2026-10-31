namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoEnFechasFunction;

public record ResultadoSolicitudProgramacion(IReadOnlyList<FechaRespetada> FechasRespetadas);

public record FechaRespetada(DateOnly Fecha, string Motivo);
