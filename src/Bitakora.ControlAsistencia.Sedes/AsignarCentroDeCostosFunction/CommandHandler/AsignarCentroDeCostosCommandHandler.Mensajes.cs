using System.Resources;

namespace Bitakora.ControlAsistencia.Sedes.AsignarCentroDeCostosFunction.CommandHandler;

public partial class AsignarCentroDeCostosCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Sedes.AsignarCentroDeCostosFunction.CommandHandler.AsignarCentroDeCostosCommandHandlerMensajes",
        typeof(AsignarCentroDeCostosCommandHandler).Assembly);

    // internal, no private: los tests lo afirman via InternalsVisibleTo.
    internal static class Mensajes
    {
        public static string SedeNoEncontrada =>
            ResourceManager.GetString(nameof(SedeNoEncontrada))!;
    }
}
