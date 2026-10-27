using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.QuitarJornadaDePlantilla;

public partial class QuitarJornadaDePlantillaTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.QuitarJornadaDePlantilla.QuitarJornadaDePlantillaToolMensajes",
        typeof(QuitarJornadaDePlantillaTool).Assembly);

    internal static class Mensajes
    {
        public static string PlantillaIdInvalido =>
            ResourceManager.GetString(nameof(PlantillaIdInvalido))!;

        public static string PlantillaNoExiste =>
            ResourceManager.GetString(nameof(PlantillaNoExiste))!;

        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string ResultadoJornadaQuitada =>
            ResourceManager.GetString(nameof(ResultadoJornadaQuitada))!;

        public static string NotaVisibilidadEventual =>
            ResourceManager.GetString(nameof(NotaVisibilidadEventual))!;
    }
}
