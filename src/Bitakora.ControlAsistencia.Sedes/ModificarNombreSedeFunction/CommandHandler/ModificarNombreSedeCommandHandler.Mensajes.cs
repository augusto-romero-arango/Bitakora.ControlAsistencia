using System.Resources;

namespace Bitakora.ControlAsistencia.Sedes.ModificarNombreSedeFunction.CommandHandler;

public partial class ModificarNombreSedeCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Sedes.ModificarNombreSedeFunction.CommandHandler.ModificarNombreSedeCommandHandlerMensajes",
        typeof(ModificarNombreSedeCommandHandler).Assembly);

    // internal, no private: los tests afirman el mensaje del 404 via InternalsVisibleTo.
    internal static class Mensajes
    {
        public static string SedeNoEncontrada =>
            ResourceManager.GetString(nameof(SedeNoEncontrada))!;
    }
}
