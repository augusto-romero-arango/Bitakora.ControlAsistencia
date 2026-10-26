using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.RetirarEtiqueta;

public partial class RetirarEtiquetaTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.RetirarEtiqueta.RetirarEtiquetaToolMensajes",
        typeof(RetirarEtiquetaTool).Assembly);

    internal static class Mensajes
    {
        public static string CampoObligatorio =>
            ResourceManager.GetString(nameof(CampoObligatorio))!;

        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string ResultadoEtiquetaRetirada =>
            ResourceManager.GetString(nameof(ResultadoEtiquetaRetirada))!;

        public static string NotaVisibilidadEventual =>
            ResourceManager.GetString(nameof(NotaVisibilidadEventual))!;
    }
}
