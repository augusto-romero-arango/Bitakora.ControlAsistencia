using System.Resources;

namespace Bitakora.ControlAsistencia.Colaboradores.ReingresarColaboradorFunction.CommandHandler;

// MEF-ADR-0009: mensajes del handler en .resx separado.
// internal: accesible desde tests via InternalsVisibleTo en el .csproj.
public partial class ReingresarColaboradorCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Colaboradores.ReingresarColaboradorFunction.CommandHandler.ReingresarColaboradorCommandHandlerMensajes",
        typeof(ReingresarColaboradorCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string ColaboradorNoEncontrado =>
            ResourceManager.GetString(nameof(ColaboradorNoEncontrado))!;

        public static string VinculacionAbierta =>
            ResourceManager.GetString(nameof(VinculacionAbierta))!;

        public static string FechaSolapaVinculacionAnterior =>
            ResourceManager.GetString(nameof(FechaSolapaVinculacionAnterior))!;
    }
}
