using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.DesmarcarDescansoAcumuladoDeJornada;

public partial class DesmarcarDescansoAcumuladoDeJornadaTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.DesmarcarDescansoAcumuladoDeJornada.DesmarcarDescansoAcumuladoDeJornadaToolMensajes",
        typeof(DesmarcarDescansoAcumuladoDeJornadaTool).Assembly);

    internal static class Mensajes
    {
        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string JornadaIdInvalido =>
            ResourceManager.GetString(nameof(JornadaIdInvalido))!;

    }
}
