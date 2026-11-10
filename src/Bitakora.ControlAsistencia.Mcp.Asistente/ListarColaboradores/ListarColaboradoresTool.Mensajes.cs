using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ListarColaboradores;

public partial class ListarColaboradoresTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.ListarColaboradores.ListarColaboradoresToolMensajes",
        typeof(ListarColaboradoresTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: identificacion pedida.</summary>
        public static string ColaboradorNoExiste =>
            ResourceManager.GetString(nameof(ColaboradorNoExiste))!;

        /// <summary>{0}: valor recibido de fecha_referencia.</summary>
        public static string FechaInvalida =>
            ResourceManager.GetString(nameof(FechaInvalida))!;

        /// <summary>{0}: cuerpo de la respuesta 400/422 del dominio.</summary>
        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string NotaHayMasColaboradores =>
            ResourceManager.GetString(nameof(NotaHayMasColaboradores))!;

        /// <summary>{0}: fecha de referencia aplicada, {1}: criterios de sede/etiquetas usados (puede ir vacio).</summary>
        public static string SinColaboradores =>
            ResourceManager.GetString(nameof(SinColaboradores))!;
    }
}
