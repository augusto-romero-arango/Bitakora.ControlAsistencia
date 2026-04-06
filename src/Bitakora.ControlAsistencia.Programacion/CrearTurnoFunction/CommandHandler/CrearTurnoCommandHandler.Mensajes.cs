using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.CommandHandler;

// ADR-0012: mensajes del handler en .resx separado
// internal: accesible desde tests via InternalsVisibleTo en el .csproj
public partial class CrearTurnoCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.CommandHandler.CrearTurnoCommandHandlerMensajes",
        typeof(CrearTurnoCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string TurnoYaExiste =>
            ResourceManager.GetString(nameof(TurnoYaExiste))!;
    }
}
