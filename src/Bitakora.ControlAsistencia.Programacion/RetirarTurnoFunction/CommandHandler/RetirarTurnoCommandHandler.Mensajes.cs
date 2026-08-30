using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.RetirarTurnoFunction.CommandHandler;

public partial class RetirarTurnoCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.RetirarTurnoFunction.CommandHandler.RetirarTurnoCommandHandlerMensajes",
        typeof(RetirarTurnoCommandHandler).Assembly);

    // internal, no private: los tests los afirman via InternalsVisibleTo.
    internal static class Mensajes
    {
        public static string TurnoNoEncontrado =>
            ResourceManager.GetString(nameof(TurnoNoEncontrado))!;

        public static string TurnoYaRetirado =>
            ResourceManager.GetString(nameof(TurnoYaRetirado))!;
    }
}
