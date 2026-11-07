using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.QuitarModalidadDeJornada;

public partial class QuitarModalidadDeJornadaTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.QuitarModalidadDeJornada.QuitarModalidadDeJornadaToolMensajes",
        typeof(QuitarModalidadDeJornadaTool).Assembly);

    internal static class Mensajes
    {
        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string JornadaIdInvalido =>
            ResourceManager.GetString(nameof(JornadaIdInvalido))!;

    }
}
