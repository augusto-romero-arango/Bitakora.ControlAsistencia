using System.Resources;

namespace Bitakora.ControlAsistencia.Sedes.ActualizarUbicacionSedeFunction.CommandHandler;

public partial class ActualizarUbicacionSedeCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Sedes.ActualizarUbicacionSedeFunction.CommandHandler.ActualizarUbicacionSedeCommandHandlerMensajes",
        typeof(ActualizarUbicacionSedeCommandHandler).Assembly);

    // internal, no private: los tests afirman el mensaje del 404 via InternalsVisibleTo.
    internal static class Mensajes
    {
        public static string SedeNoEncontrada =>
            ResourceManager.GetString(nameof(SedeNoEncontrada))!;
    }
}
