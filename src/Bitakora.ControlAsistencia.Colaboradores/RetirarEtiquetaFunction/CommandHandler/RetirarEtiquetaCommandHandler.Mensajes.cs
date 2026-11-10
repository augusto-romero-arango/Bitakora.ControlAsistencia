using System.Resources;

namespace Bitakora.ControlAsistencia.Colaboradores.RetirarEtiquetaFunction.CommandHandler;

public partial class RetirarEtiquetaCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Colaboradores.RetirarEtiquetaFunction.CommandHandler.RetirarEtiquetaCommandHandlerMensajes",
        typeof(RetirarEtiquetaCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string ColaboradorNoEncontrado =>
            ResourceManager.GetString(nameof(ColaboradorNoEncontrado))!;

        public static string VinculacionTerminada =>
            ResourceManager.GetString(nameof(VinculacionTerminada))!;
    }
}
