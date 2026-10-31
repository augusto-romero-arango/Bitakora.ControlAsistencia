using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoEnFechasFunction.CommandHandler;

public partial class SolicitarProgramacionTurnoEnFechasCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoEnFechasFunction.CommandHandler.SolicitarProgramacionTurnoEnFechasCommandHandlerMensajes",
        typeof(SolicitarProgramacionTurnoEnFechasCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string SolicitudYaExiste =>
            ResourceManager.GetString(nameof(SolicitudYaExiste))!;

        public static string TurnoNoEncontrado =>
            ResourceManager.GetString(nameof(TurnoNoEncontrado))!;

        public static string TurnoRetirado =>
            ResourceManager.GetString(nameof(TurnoRetirado))!;

        public static string TodasLasFechasConAusencia =>
            ResourceManager.GetString(nameof(TodasLasFechasConAusencia))!;

        public static string JornadaNoEncontrada =>
            ResourceManager.GetString(nameof(JornadaNoEncontrada))!;

        public static string TurnoIncompleto =>
            ResourceManager.GetString(nameof(TurnoIncompleto))!;
    }
}
