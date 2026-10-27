using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarJornadaAPlantilla;

public partial class AsignarJornadaAPlantillaTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.AsignarJornadaAPlantilla.AsignarJornadaAPlantillaToolMensajes",
        typeof(AsignarJornadaAPlantillaTool).Assembly);

    internal static class Mensajes
    {
        public static string PlantillaIdInvalido =>
            ResourceManager.GetString(nameof(PlantillaIdInvalido))!;

        public static string JornadaIdInvalido =>
            ResourceManager.GetString(nameof(JornadaIdInvalido))!;

        public static string PlantillaNoExiste =>
            ResourceManager.GetString(nameof(PlantillaNoExiste))!;

        public static string JornadaNoExiste =>
            ResourceManager.GetString(nameof(JornadaNoExiste))!;

        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string ResultadoJornadaAsignada =>
            ResourceManager.GetString(nameof(ResultadoJornadaAsignada))!;

        public static string NotaAdvertenciasEventuales =>
            ResourceManager.GetString(nameof(NotaAdvertenciasEventuales))!;

    }
}
