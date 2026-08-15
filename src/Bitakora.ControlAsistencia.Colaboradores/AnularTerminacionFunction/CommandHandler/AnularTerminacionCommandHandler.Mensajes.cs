using System.Resources;

namespace Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction.CommandHandler;

// MEF-ADR-0009: mensajes del handler en .resx separado.
// internal: accesible desde tests via InternalsVisibleTo en el .csproj.
public partial class AnularTerminacionCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction.CommandHandler.AnularTerminacionCommandHandlerMensajes",
        typeof(AnularTerminacionCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string ColaboradorNoEncontrado =>
            ResourceManager.GetString(nameof(ColaboradorNoEncontrado))!;

        public static string VinculacionAbierta =>
            ResourceManager.GetString(nameof(VinculacionAbierta))!;

        // Issue #379 (MEF-ADR-0043 paso 4, CA-5): el {codigo} de la ruta no corresponde al codigo
        // de la vinculacion vigente -- evaluada PRIMERA por el aggregate.
        public static string CodigoNoCorresponde =>
            ResourceManager.GetString(nameof(CodigoNoCorresponde))!;
    }
}
