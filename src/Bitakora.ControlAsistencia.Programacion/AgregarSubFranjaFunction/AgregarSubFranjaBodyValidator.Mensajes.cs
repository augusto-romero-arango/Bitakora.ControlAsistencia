using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction;

// MEF-ADR-0009: el nombre logico del recurso debe coincidir con el .resx co-localizado.
public partial class AgregarSubFranjaBodyValidator
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction.AgregarSubFranjaBodyValidatorMensajes",
        typeof(AgregarSubFranjaBodyValidator).Assembly);

    internal static class Mensajes
    {
        public static string TipoDesconocido =>
            ResourceManager.GetString(nameof(TipoDesconocido))!;
    }
}
