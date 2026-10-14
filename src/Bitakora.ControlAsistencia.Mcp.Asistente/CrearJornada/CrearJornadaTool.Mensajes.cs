using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.CrearJornada;

public partial class CrearJornadaTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.CrearJornada.CrearJornadaToolMensajes",
        typeof(CrearJornadaTool).Assembly);

    internal static class Mensajes
    {
        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string DuracionInvalida =>
            ResourceManager.GetString(nameof(DuracionInvalida))!;

        public static string DiasDescansoInvalido =>
            ResourceManager.GetString(nameof(DiasDescansoInvalido))!;

    }
}
