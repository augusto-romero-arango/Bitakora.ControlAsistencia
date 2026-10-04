using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarAusencias;

public partial class ConsultarAusenciasTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarAusencias.ConsultarAusenciasToolMensajes",
        typeof(ConsultarAusenciasTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: nombre del parametro, {1}: valor recibido.</summary>
        public static string FechaInvalida =>
            ResourceManager.GetString(nameof(FechaInvalida))!;

        public static string DesdePosteriorAHasta =>
            ResourceManager.GetString(nameof(DesdePosteriorAHasta))!;

        /// <summary>{0}: cuerpo de la respuesta 400/422 del dominio.</summary>
        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        /// <summary>{0}: desde aplicado, {1}: hasta aplicado.</summary>
        public static string NadieFalta =>
            ResourceManager.GetString(nameof(NadieFalta))!;

        /// <summary>{0}: desde aplicado, {1}: hasta aplicado.</summary>
        public static string NotaRecorte =>
            ResourceManager.GetString(nameof(NotaRecorte))!;

        /// <summary>{0}: colaboradores mostrados, {1}: total, {2}: omitidos.</summary>
        public static string NotaTruncado =>
            ResourceManager.GetString(nameof(NotaTruncado))!;
    }
}
