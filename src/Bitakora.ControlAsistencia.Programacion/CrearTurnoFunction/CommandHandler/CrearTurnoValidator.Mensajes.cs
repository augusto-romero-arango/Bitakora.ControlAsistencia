using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.CommandHandler;

// MEF-ADR-0009: el nombre logico del recurso debe coincidir con el .resx co-localizado.
public partial class CrearTurnoValidator
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.CommandHandler.CrearTurnoValidatorMensajes",
        typeof(CrearTurnoValidator).Assembly);

    internal static class Mensajes
    {
        public static string EsDescansoConFranjas =>
            ResourceManager.GetString(nameof(EsDescansoConFranjas))!;
    }
}
