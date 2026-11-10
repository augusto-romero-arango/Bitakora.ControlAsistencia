using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.RetirarTurnoFunction.CommandHandler;

public partial class RetirarTurnoCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.RetirarTurnoFunction.CommandHandler.RetirarTurnoCommandHandlerMensajes",
        typeof(RetirarTurnoCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string TurnoNoEncontrado =>
            ResourceManager.GetString(nameof(TurnoNoEncontrado))!;
    }
}
