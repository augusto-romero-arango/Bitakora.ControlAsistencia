using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.BuscarTurnos;

public partial class BuscarTurnosTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.BuscarTurnos.BuscarTurnosToolMensajes",
        typeof(BuscarTurnosTool).Assembly);

    internal static class Mensajes
    {
        public static string FaltaCriterio => ResourceManager.GetString(nameof(FaltaCriterio))!;

        /// <summary>{0}: horario recibido.</summary>
        public static string HorarioMalFormado => ResourceManager.GetString(nameof(HorarioMalFormado))!;

        /// <summary>{0}: criterios usados.</summary>
        public static string SinCoincidencias => ResourceManager.GetString(nameof(SinCoincidencias))!;

        public static string NotaTruncado => ResourceManager.GetString(nameof(NotaTruncado))!;

        /// <summary>{0}: palabras coincidentes.</summary>
        public static string CoincidePorNombre => ResourceManager.GetString(nameof(CoincidePorNombre))!;

        /// <summary>{0}: franja.</summary>
        public static string CoincidePorHorarioExacto => ResourceManager.GetString(nameof(CoincidePorHorarioExacto))!;

        /// <summary>{0}: franja.</summary>
        public static string CoincidePorHorarioCercano => ResourceManager.GetString(nameof(CoincidePorHorarioCercano))!;

        /// <summary>{0}: franja.</summary>
        public static string CoincidePorHorarioSeCruza => ResourceManager.GetString(nameof(CoincidePorHorarioSeCruza))!;

        /// <summary>{0}: sede.</summary>
        public static string CoincidePorSede => ResourceManager.GetString(nameof(CoincidePorSede))!;

        /// <summary>{0}: palabra pedida; {1}: palabra encontrada.</summary>
        public static string PalabraAproximada => ResourceManager.GetString(nameof(PalabraAproximada))!;

        public static string CriterioNombre => ResourceManager.GetString(nameof(CriterioNombre))!;

        public static string CriterioHorario => ResourceManager.GetString(nameof(CriterioHorario))!;

        public static string CriterioSede => ResourceManager.GetString(nameof(CriterioSede))!;
    }
}
