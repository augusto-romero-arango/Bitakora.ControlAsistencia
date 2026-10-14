using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarMallaTurnos;

public partial class ConsultarMallaTurnosTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarMallaTurnos.ConsultarMallaTurnosToolMensajes",
        typeof(ConsultarMallaTurnosTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: valor recibido.</summary>
        public static string FechaInvalida => ResourceManager.GetString(nameof(FechaInvalida))!;

        /// <summary>{0}: cuerpo de la respuesta 400/422 del dominio.</summary>
        public static string RechazoDelDominio => ResourceManager.GetString(nameof(RechazoDelDominio))!;

        /// <summary>{0}: criterio del grupo, {1}: semana.</summary>
        public static string GrupoSinColaboradores => ResourceManager.GetString(nameof(GrupoSinColaboradores))!;

        /// <summary>{0}: cursor a reenviar.</summary>
        public static string NotaMasColaboradores => ResourceManager.GetString(nameof(NotaMasColaboradores))!;

        public static string SemanaMismoMes => ResourceManager.GetString(nameof(SemanaMismoMes))!;

        public static string SemanaEntreMeses => ResourceManager.GetString(nameof(SemanaEntreMeses))!;

        public static string CriterioSede => ResourceManager.GetString(nameof(CriterioSede))!;

        public static string CriterioEtiquetas => ResourceManager.GetString(nameof(CriterioEtiquetas))!;

        public static string CriterioCodigos => ResourceManager.GetString(nameof(CriterioCodigos))!;

        public static string CriterioTodos => ResourceManager.GetString(nameof(CriterioTodos))!;

        public static string DiaTrabajo => ResourceManager.GetString(nameof(DiaTrabajo))!;

        public static string DiaDescanso => ResourceManager.GetString(nameof(DiaDescanso))!;

        public static string DiaAusencia => ResourceManager.GetString(nameof(DiaAusencia))!;

        public static string DiaSinProgramar => ResourceManager.GetString(nameof(DiaSinProgramar))!;

        public static string DiaFueraDeVinculacion => ResourceManager.GetString(nameof(DiaFueraDeVinculacion))!;
    }
}
