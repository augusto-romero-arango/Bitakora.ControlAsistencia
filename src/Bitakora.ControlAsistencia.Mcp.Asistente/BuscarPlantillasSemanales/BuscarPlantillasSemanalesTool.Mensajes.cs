using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.BuscarPlantillasSemanales;

public partial class BuscarPlantillasSemanalesTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.BuscarPlantillasSemanales.BuscarPlantillasSemanalesToolMensajes",
        typeof(BuscarPlantillasSemanalesTool).Assembly);

    internal static class Mensajes
    {
        public static string FaltaCriterio => ResourceManager.GetString(nameof(FaltaCriterio))!;

        public static string SemanasInvalidas => ResourceManager.GetString(nameof(SemanasInvalidas))!;

        public static string EstadoDesconocido => ResourceManager.GetString(nameof(EstadoDesconocido))!;

        public static string SinCoincidencias => ResourceManager.GetString(nameof(SinCoincidencias))!;

        public static string NotaTruncado => ResourceManager.GetString(nameof(NotaTruncado))!;

        public static string CoincidePorNombre => ResourceManager.GetString(nameof(CoincidePorNombre))!;

        public static string CoincidePorTurno => ResourceManager.GetString(nameof(CoincidePorTurno))!;

        public static string CoincidePorSemanas => ResourceManager.GetString(nameof(CoincidePorSemanas))!;

        public static string CoincidePorIncompleta => ResourceManager.GetString(nameof(CoincidePorIncompleta))!;

        public static string CoincidePorConAdvertencias => ResourceManager.GetString(nameof(CoincidePorConAdvertencias))!;

        public static string PalabraAproximada => ResourceManager.GetString(nameof(PalabraAproximada))!;

        public static string CriterioNombre => ResourceManager.GetString(nameof(CriterioNombre))!;

        public static string CriterioTurno => ResourceManager.GetString(nameof(CriterioTurno))!;

        public static string CriterioSemanas => ResourceManager.GetString(nameof(CriterioSemanas))!;

        public static string CriterioEstado => ResourceManager.GetString(nameof(CriterioEstado))!;

        public static string DiaLunes => ResourceManager.GetString(nameof(DiaLunes))!;

        public static string DiaMartes => ResourceManager.GetString(nameof(DiaMartes))!;

        public static string DiaMiercoles => ResourceManager.GetString(nameof(DiaMiercoles))!;

        public static string DiaJueves => ResourceManager.GetString(nameof(DiaJueves))!;

        public static string DiaViernes => ResourceManager.GetString(nameof(DiaViernes))!;

        public static string DiaSabado => ResourceManager.GetString(nameof(DiaSabado))!;

        public static string DiaDomingo => ResourceManager.GetString(nameof(DiaDomingo))!;
    }
}
