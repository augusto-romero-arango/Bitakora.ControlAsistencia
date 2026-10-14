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

        /// <summary>{0}: desde aplicado, {1}: hasta aplicado, {2}: criterio entre parentesis o vacio.</summary>
        public static string NadieFalta =>
            ResourceManager.GetString(nameof(NadieFalta))!;

        /// <summary>{0}: desde aplicado, {1}: hasta aplicado.</summary>
        public static string NotaRecorte =>
            ResourceManager.GetString(nameof(NotaRecorte))!;

        /// <summary>{0}: cursor del backend.</summary>
        public static string NotaMasColaboradores =>
            ResourceManager.GetString(nameof(NotaMasColaboradores))!;

        /// <summary>{0}: criterio, {1}: desde pedido, {2}: hasta pedido.</summary>
        public static string GrupoSinColaboradores =>
            ResourceManager.GetString(nameof(GrupoSinColaboradores))!;

        public static string CriterioSede => ResourceManager.GetString(nameof(CriterioSede))!;

        public static string CriterioEtiquetas => ResourceManager.GetString(nameof(CriterioEtiquetas))!;

        public static string CriterioCodigos => ResourceManager.GetString(nameof(CriterioCodigos))!;
    }
}
