using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.MarcarDescansoAcumuladoDeJornada;

public partial class MarcarDescansoAcumuladoDeJornadaTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.MarcarDescansoAcumuladoDeJornada.MarcarDescansoAcumuladoDeJornadaToolMensajes",
        typeof(MarcarDescansoAcumuladoDeJornadaTool).Assembly);

    internal static class Mensajes
    {
        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string JornadaIdInvalido =>
            ResourceManager.GetString(nameof(JornadaIdInvalido))!;

    }
}
