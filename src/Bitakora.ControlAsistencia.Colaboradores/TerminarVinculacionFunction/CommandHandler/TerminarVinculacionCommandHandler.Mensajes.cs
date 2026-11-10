using System.Resources;

namespace Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction.CommandHandler;

public partial class TerminarVinculacionCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction.CommandHandler.TerminarVinculacionCommandHandlerMensajes",
        typeof(TerminarVinculacionCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string ColaboradorNoEncontrado =>
            ResourceManager.GetString(nameof(ColaboradorNoEncontrado))!;

        public static string VinculacionYaTerminada =>
            ResourceManager.GetString(nameof(VinculacionYaTerminada))!;

        public static string FechaAnteriorAInicio =>
            ResourceManager.GetString(nameof(FechaAnteriorAInicio))!;

        public static string CodigoNoCorresponde =>
            ResourceManager.GetString(nameof(CodigoNoCorresponde))!;
    }
}
