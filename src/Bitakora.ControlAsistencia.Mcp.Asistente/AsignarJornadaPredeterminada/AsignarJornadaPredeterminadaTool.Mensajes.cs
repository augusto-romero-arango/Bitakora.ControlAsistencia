using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarJornadaPredeterminada;

public partial class AsignarJornadaPredeterminadaTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.AsignarJornadaPredeterminada.AsignarJornadaPredeterminadaToolMensajes",
        typeof(AsignarJornadaPredeterminadaTool).Assembly);

    internal static class Mensajes
    {
        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string JornadaIdInvalido =>
            ResourceManager.GetString(nameof(JornadaIdInvalido))!;

    }
}
