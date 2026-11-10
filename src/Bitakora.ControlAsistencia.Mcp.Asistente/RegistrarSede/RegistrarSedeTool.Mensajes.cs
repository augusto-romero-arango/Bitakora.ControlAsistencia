using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.RegistrarSede;

public partial class RegistrarSedeTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.RegistrarSede.RegistrarSedeToolMensajes",
        typeof(RegistrarSedeTool).Assembly);

    internal static class Mensajes
    {
        public static string ResultadoSedeRegistrada =>
            ResourceManager.GetString(nameof(ResultadoSedeRegistrada))!;

        /// <summary>{0}: nombre del campo obligatorio en blanco.</summary>
        public static string CampoObligatorio =>
            ResourceManager.GetString(nameof(CampoObligatorio))!;

        /// <summary>{0}: cuerpo de la respuesta 400/409 del dominio.</summary>
        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string NotaVisibilidadEventual =>
            ResourceManager.GetString(nameof(NotaVisibilidadEventual))!;
    }
}
