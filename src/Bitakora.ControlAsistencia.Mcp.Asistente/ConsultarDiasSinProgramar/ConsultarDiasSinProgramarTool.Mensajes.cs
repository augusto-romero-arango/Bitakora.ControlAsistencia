using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarDiasSinProgramar;

public partial class ConsultarDiasSinProgramarTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarDiasSinProgramar.ConsultarDiasSinProgramarToolMensajes",
        typeof(ConsultarDiasSinProgramarTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: valor recibido.</summary>
        public static string FechaInvalida => ResourceManager.GetString(nameof(FechaInvalida))!;

        /// <summary>{0}: valor recibido.</summary>
        public static string SemanasFueraDeRango => ResourceManager.GetString(nameof(SemanasFueraDeRango))!;

        /// <summary>{0}: cuerpo de la respuesta 400/422 del dominio.</summary>
        public static string RechazoDelDominio => ResourceManager.GetString(nameof(RechazoDelDominio))!;

        /// <summary>{0}: periodo, {1}: criterio (" (sede Norte)") o vacio.</summary>
        public static string NadieTieneDiasSinProgramar => ResourceManager.GetString(nameof(NadieTieneDiasSinProgramar))!;

        /// <summary>{0}: criterio del grupo, {1}: periodo.</summary>
        public static string GrupoSinColaboradores => ResourceManager.GetString(nameof(GrupoSinColaboradores))!;

        /// <summary>{0}: cursor a reenviar.</summary>
        public static string NotaMasColaboradores => ResourceManager.GetString(nameof(NotaMasColaboradores))!;

        public static string SemanaMismoMes => ResourceManager.GetString(nameof(SemanaMismoMes))!;

        public static string SemanaEntreMeses => ResourceManager.GetString(nameof(SemanaEntreMeses))!;

        public static string CriterioSede => ResourceManager.GetString(nameof(CriterioSede))!;

        public static string CriterioEtiquetas => ResourceManager.GetString(nameof(CriterioEtiquetas))!;

        public static string CriterioCodigos => ResourceManager.GetString(nameof(CriterioCodigos))!;
    }
}
