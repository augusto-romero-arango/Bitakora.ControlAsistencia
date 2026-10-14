using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.QuitarJornadaDePlantillaSemanal;

public partial class QuitarJornadaDePlantillaSemanalTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.QuitarJornadaDePlantillaSemanal.QuitarJornadaDePlantillaSemanalToolMensajes",
        typeof(QuitarJornadaDePlantillaSemanalTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: nombre del campo obligatorio en blanco.</summary>
        public static string CampoObligatorio =>
            ResourceManager.GetString(nameof(CampoObligatorio))!;

        /// <summary>{0}: nombre de plantilla recibido; {1}: nombres disponibles.</summary>
        public static string PlantillaNoExiste =>
            ResourceManager.GetString(nameof(PlantillaNoExiste))!;

        /// <summary>{0}: cuerpo de la respuesta 404/409/5xx del dominio.</summary>
        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string ResultadoJornadaQuitada =>
            ResourceManager.GetString(nameof(ResultadoJornadaQuitada))!;

        public static string NotaVisibilidadEventual =>
            ResourceManager.GetString(nameof(NotaVisibilidadEventual))!;
    }
}
