using System.Resources;

namespace Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction.CommandHandler;

// MEF-ADR-0009: mensajes del handler en .resx separado.
// internal: accesible desde tests via InternalsVisibleTo en el .csproj.
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
