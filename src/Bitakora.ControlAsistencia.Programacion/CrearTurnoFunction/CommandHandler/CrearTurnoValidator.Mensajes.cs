using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.CommandHandler;

// MEF-ADR-0009: mensajes del validator en .resx separado
// internal: accesible desde tests via InternalsVisibleTo en el .csproj
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
