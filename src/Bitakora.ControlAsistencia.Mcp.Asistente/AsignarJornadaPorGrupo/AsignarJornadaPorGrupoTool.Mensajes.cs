using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarJornadaPorGrupo;

public partial class AsignarJornadaPorGrupoTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.AsignarJornadaPorGrupo.AsignarJornadaPorGrupoToolMensajes",
        typeof(AsignarJornadaPorGrupoTool).Assembly);

    internal static class Mensajes
    {
        public static string SelectorObligatorio =>
            ResourceManager.GetString(nameof(SelectorObligatorio))!;

        public static string EtiquetaMalFormada =>
            ResourceManager.GetString(nameof(EtiquetaMalFormada))!;

        public static string SedeDelSelectorNoExiste =>
            ResourceManager.GetString(nameof(SedeDelSelectorNoExiste))!;

        public static string SedeDelSelectorInactiva =>
            ResourceManager.GetString(nameof(SedeDelSelectorInactiva))!;

        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string GrupoSinColaboradores =>
            ResourceManager.GetString(nameof(GrupoSinColaboradores))!;
    }
}
