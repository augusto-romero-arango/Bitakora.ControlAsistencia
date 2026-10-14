using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ModificarLimitesJornada;

public partial class ModificarLimitesJornadaTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.ModificarLimitesJornada.ModificarLimitesJornadaToolMensajes",
        typeof(ModificarLimitesJornadaTool).Assembly);

    internal static class Mensajes
    {
        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string DuracionInvalida =>
            ResourceManager.GetString(nameof(DuracionInvalida))!;

        public static string DiasDescansoInvalido =>
            ResourceManager.GetString(nameof(DiasDescansoInvalido))!;

        public static string JornadaIdInvalido =>
            ResourceManager.GetString(nameof(JornadaIdInvalido))!;

    }
}
