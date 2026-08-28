using System.Resources;

namespace Bitakora.ControlAsistencia.Sedes.ActualizarUbicacionSedeFunction.CommandHandler;

// MEF-ADR-0009: mensajes del handler en .resx separado.
// internal: accesible desde tests via InternalsVisibleTo en el .csproj.
public partial class ActualizarUbicacionSedeCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Sedes.ActualizarUbicacionSedeFunction.CommandHandler.ActualizarUbicacionSedeCommandHandlerMensajes",
        typeof(ActualizarUbicacionSedeCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string SedeNoEncontrada =>
            ResourceManager.GetString(nameof(SedeNoEncontrada))!;
    }
}
