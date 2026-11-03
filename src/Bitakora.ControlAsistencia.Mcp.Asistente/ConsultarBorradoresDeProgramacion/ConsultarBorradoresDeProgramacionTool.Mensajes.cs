using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarBorradoresDeProgramacion;

public partial class ConsultarBorradoresDeProgramacionTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarBorradoresDeProgramacion.ConsultarBorradoresDeProgramacionToolMensajes",
        typeof(ConsultarBorradoresDeProgramacionTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: nombre del parametro, {1}: valor recibido.</summary>
        public static string FechaInvalida => ResourceManager.GetString(nameof(FechaInvalida))!;

        public static string RangoIncompleto => ResourceManager.GetString(nameof(RangoIncompleto))!;

        public static string DesdePosteriorAHasta => ResourceManager.GetString(nameof(DesdePosteriorAHasta))!;

        /// <summary>{0}: rechazo del dominio.</summary>
        public static string RechazoDelDominio => ResourceManager.GetString(nameof(RechazoDelDominio))!;

        /// <summary>{0}: criterio entre parentesis o vacio, {1}: periodo (" entre X y Y") o vacio.</summary>
        public static string SinBorradores => ResourceManager.GetString(nameof(SinBorradores))!;

        /// <summary>{0}: criterio.</summary>
        public static string GrupoSinColaboradores => ResourceManager.GetString(nameof(GrupoSinColaboradores))!;

        public static string NotaMasColaboradores => ResourceManager.GetString(nameof(NotaMasColaboradores))!;

        public static string CriterioSede => ResourceManager.GetString(nameof(CriterioSede))!;

        public static string CriterioEtiquetas => ResourceManager.GetString(nameof(CriterioEtiquetas))!;

        public static string CriterioCodigos => ResourceManager.GetString(nameof(CriterioCodigos))!;
    }
}
