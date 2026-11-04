using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.DescartarBorradorDeProgramacionFunction.CommandHandler;

public partial class DescartarBorradorDeProgramacionBodyValidator
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.DescartarBorradorDeProgramacionFunction.CommandHandler.DescartarBorradorDeProgramacionBodyValidatorMensajes",
        typeof(DescartarBorradorDeProgramacionBodyValidator).Assembly);

    internal static class Mensajes
    {
        public static string DesdeRequerido => ResourceManager.GetString(nameof(DesdeRequerido))!;
        public static string HastaRequerido => ResourceManager.GetString(nameof(HastaRequerido))!;
        public static string DesdeMayorQueHasta => ResourceManager.GetString(nameof(DesdeMayorQueHasta))!;
        public static string RangoExcedeElMaximo => ResourceManager.GetString(nameof(RangoExcedeElMaximo))!;
    }
}
