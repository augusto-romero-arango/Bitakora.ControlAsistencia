using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarAdvertenciasProgramacion;

public partial class ConsultarAdvertenciasProgramacionTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarAdvertenciasProgramacion.ConsultarAdvertenciasProgramacionToolMensajes",
        typeof(ConsultarAdvertenciasProgramacionTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: valor recibido.</summary>
        public static string FechaInvalida => ResourceManager.GetString(nameof(FechaInvalida))!;

        /// <summary>{0}: cuerpo de la respuesta 400/422 del dominio.</summary>
        public static string RechazoDelDominio => ResourceManager.GetString(nameof(RechazoDelDominio))!;

        /// <summary>{0}: semana ("12 al 18 de octubre de 2026"), {1}: criterio (" (sede Norte)") o vacio.</summary>
        public static string NadieTieneAdvertencias => ResourceManager.GetString(nameof(NadieTieneAdvertencias))!;

        /// <summary>{0}: criterio del grupo, {1}: semana.</summary>
        public static string GrupoSinColaboradores => ResourceManager.GetString(nameof(GrupoSinColaboradores))!;

        /// <summary>{0}: cursor a reenviar.</summary>
        public static string NotaMasColaboradores => ResourceManager.GetString(nameof(NotaMasColaboradores))!;

        /// <summary>{0}: dia del lunes, {1}: dia del domingo, {2}: mes, {3}: anio.</summary>
        public static string SemanaMismoMes => ResourceManager.GetString(nameof(SemanaMismoMes))!;

        /// <summary>{0}: dia del lunes, {1}: mes del lunes, {2}: dia del domingo, {3}: mes del domingo, {4}: anio.</summary>
        public static string SemanaEntreMeses => ResourceManager.GetString(nameof(SemanaEntreMeses))!;

        public static string CriterioSede => ResourceManager.GetString(nameof(CriterioSede))!;

        public static string CriterioEtiquetas => ResourceManager.GetString(nameof(CriterioEtiquetas))!;

        public static string CriterioCodigos => ResourceManager.GetString(nameof(CriterioCodigos))!;
    }
}
