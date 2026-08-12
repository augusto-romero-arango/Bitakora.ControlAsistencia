using System.Resources;

namespace Bitakora.ControlAsistencia.Colaboradores.RetirarEtiquetaFunction.CommandHandler;

// MEF-ADR-0009: mensajes del handler en .resx separado.
// internal: accesible desde tests via InternalsVisibleTo en el .csproj.
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

        public static string CategoriaInexistente =>
            ResourceManager.GetString(nameof(CategoriaInexistente))!;
    }
}
