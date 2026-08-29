using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.CommandHandler;

// Mensajes internal, no private: los tests los leen via InternalsVisibleTo del .csproj.
public partial class CrearTurnoCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.CommandHandler.CrearTurnoCommandHandlerMensajes",
        typeof(CrearTurnoCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string TurnoYaExiste =>
            ResourceManager.GetString(nameof(TurnoYaExiste))!;

        public static string NombreDuplicado =>
            ResourceManager.GetString(nameof(NombreDuplicado))!;
    }
}
