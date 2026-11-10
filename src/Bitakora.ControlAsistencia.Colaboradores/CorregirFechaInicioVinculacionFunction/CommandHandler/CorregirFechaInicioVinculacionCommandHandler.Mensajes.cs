using System.Resources;

namespace Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction.CommandHandler;

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

        public static string CodigoNoCorresponde =>
            ResourceManager.GetString(nameof(CodigoNoCorresponde))!;
    }
}
