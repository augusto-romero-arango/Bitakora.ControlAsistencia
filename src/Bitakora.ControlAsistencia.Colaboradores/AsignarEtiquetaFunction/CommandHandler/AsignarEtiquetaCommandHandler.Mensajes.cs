using System.Resources;

namespace Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction.CommandHandler;

public partial class AsignarEtiquetaCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction.CommandHandler.AsignarEtiquetaCommandHandlerMensajes",
        typeof(AsignarEtiquetaCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string ColaboradorNoEncontrado =>
            ResourceManager.GetString(nameof(ColaboradorNoEncontrado))!;

        public static string VinculacionTerminada =>
            ResourceManager.GetString(nameof(VinculacionTerminada))!;
    }
}
