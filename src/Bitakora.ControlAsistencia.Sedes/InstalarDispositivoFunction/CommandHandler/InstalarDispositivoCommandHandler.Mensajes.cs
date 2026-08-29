using System.Resources;

namespace Bitakora.ControlAsistencia.Sedes.InstalarDispositivoFunction.CommandHandler;

public partial class InstalarDispositivoCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Sedes.InstalarDispositivoFunction.CommandHandler.InstalarDispositivoCommandHandlerMensajes",
        typeof(InstalarDispositivoCommandHandler).Assembly);

    // internal, no private: los tests lo afirman via InternalsVisibleTo.
    internal static class Mensajes
    {
        public static string SedeNoEncontrada =>
            ResourceManager.GetString(nameof(SedeNoEncontrada))!;

        public static string DispositivoYaInstalado =>
            ResourceManager.GetString(nameof(DispositivoYaInstalado))!;

        public static string DispositivoInstaladoEnOtraSede =>
            ResourceManager.GetString(nameof(DispositivoInstaladoEnOtraSede))!;
    }
}
