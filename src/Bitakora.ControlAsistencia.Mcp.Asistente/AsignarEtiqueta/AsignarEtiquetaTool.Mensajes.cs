using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarEtiqueta;

public partial class AsignarEtiquetaTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.AsignarEtiqueta.AsignarEtiquetaToolMensajes",
        typeof(AsignarEtiquetaTool).Assembly);

    internal static class Mensajes
    {
        public static string CampoObligatorio =>
            ResourceManager.GetString(nameof(CampoObligatorio))!;

        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string ResultadoEtiquetaAsignada =>
            ResourceManager.GetString(nameof(ResultadoEtiquetaAsignada))!;

        public static string NotaVisibilidadEventual =>
            ResourceManager.GetString(nameof(NotaVisibilidadEventual))!;
    }
}
