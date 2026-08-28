using System.Resources;

namespace Bitakora.ControlAsistencia.Sedes.RetirarDispositivoFunction.CommandHandler;

public partial class RetirarDispositivoCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Sedes.RetirarDispositivoFunction.CommandHandler.RetirarDispositivoCommandHandlerMensajes",
        typeof(RetirarDispositivoCommandHandler).Assembly);

    // internal, no private: los tests lo afirman via InternalsVisibleTo.
    internal static class Mensajes
    {
        public static string SedeNoEncontrada =>
            ResourceManager.GetString(nameof(SedeNoEncontrada))!;

        public static string DispositivoNoInstalado =>
            ResourceManager.GetString(nameof(DispositivoNoInstalado))!;
    }
}
