using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarModalidadAJornada;

public partial class AsignarModalidadAJornadaTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.AsignarModalidadAJornada.AsignarModalidadAJornadaToolMensajes",
        typeof(AsignarModalidadAJornadaTool).Assembly);

    internal static class Mensajes
    {
        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string JornadaIdInvalido =>
            ResourceManager.GetString(nameof(JornadaIdInvalido))!;

        public static string ModalidadInvalida =>
            ResourceManager.GetString(nameof(ModalidadInvalida))!;

    }
}
