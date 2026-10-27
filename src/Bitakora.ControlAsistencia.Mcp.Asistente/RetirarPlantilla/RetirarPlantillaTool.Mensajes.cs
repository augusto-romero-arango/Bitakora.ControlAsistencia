using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.RetirarPlantilla;

public partial class RetirarPlantillaTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.RetirarPlantilla.RetirarPlantillaToolMensajes",
        typeof(RetirarPlantillaTool).Assembly);

    internal static class Mensajes
    {
        public static string PlantillaIdInvalido =>
            ResourceManager.GetString(nameof(PlantillaIdInvalido))!;

        public static string PlantillaNoExiste =>
            ResourceManager.GetString(nameof(PlantillaNoExiste))!;

        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string ResultadoPlantillaRetirada =>
            ResourceManager.GetString(nameof(ResultadoPlantillaRetirada))!;

        public static string NotaVisibilidadEventual =>
            ResourceManager.GetString(nameof(NotaVisibilidadEventual))!;
    }
}
