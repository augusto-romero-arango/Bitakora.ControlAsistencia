using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction.CommandHandler;

public partial class SolicitarProgramacionTurnoCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction.CommandHandler.SolicitarProgramacionTurnoCommandHandlerMensajes",
        typeof(SolicitarProgramacionTurnoCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string SolicitudYaExiste =>
            ResourceManager.GetString(nameof(SolicitudYaExiste))!;

        public static string TurnoNoEncontrado =>
            ResourceManager.GetString(nameof(TurnoNoEncontrado))!;
    }
}
