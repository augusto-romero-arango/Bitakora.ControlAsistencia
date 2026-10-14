using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarJornada;

public partial class AsignarJornadaTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.AsignarJornada.AsignarJornadaToolMensajes",
        typeof(AsignarJornadaTool).Assembly);

    internal static class Mensajes
    {
        public static string CampoObligatorio =>
            ResourceManager.GetString(nameof(CampoObligatorio))!;

        public static string JornadaIdInvalido =>
            ResourceManager.GetString(nameof(JornadaIdInvalido))!;

        public static string JornadaNoExiste =>
            ResourceManager.GetString(nameof(JornadaNoExiste))!;

        public static string JornadaDesactivada =>
            ResourceManager.GetString(nameof(JornadaDesactivada))!;

        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string ResultadoJornadaAsignada =>
            ResourceManager.GetString(nameof(ResultadoJornadaAsignada))!;

        public static string NotaVisibilidadEventual =>
            ResourceManager.GetString(nameof(NotaVisibilidadEventual))!;
    }
}
