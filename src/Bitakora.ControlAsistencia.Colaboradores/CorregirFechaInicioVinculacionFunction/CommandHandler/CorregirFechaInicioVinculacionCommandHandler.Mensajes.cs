using System.Resources;

namespace Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction.CommandHandler;

// MEF-ADR-0009: mensajes del handler en .resx separado.
// internal: accesible desde tests via InternalsVisibleTo en el .csproj.
public partial class CorregirFechaInicioVinculacionCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction.CommandHandler.CorregirFechaInicioVinculacionCommandHandlerMensajes",
        typeof(CorregirFechaInicioVinculacionCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string ColaboradorNoEncontrado =>
            ResourceManager.GetString(nameof(ColaboradorNoEncontrado))!;

        public static string FechaPosteriorATerminacionPropia =>
            ResourceManager.GetString(nameof(FechaPosteriorATerminacionPropia))!;

        public static string FechaSolapaVinculacionAnterior =>
            ResourceManager.GetString(nameof(FechaSolapaVinculacionAnterior))!;
    }
}
