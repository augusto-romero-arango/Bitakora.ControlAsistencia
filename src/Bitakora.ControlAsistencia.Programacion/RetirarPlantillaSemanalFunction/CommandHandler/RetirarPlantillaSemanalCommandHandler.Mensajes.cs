using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.RetirarPlantillaSemanalFunction.CommandHandler;

public partial class RetirarPlantillaSemanalCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.RetirarPlantillaSemanalFunction.CommandHandler.RetirarPlantillaSemanalCommandHandlerMensajes",
        typeof(RetirarPlantillaSemanalCommandHandler).Assembly);

    // internal, no private: los tests los leen via InternalsVisibleTo del .csproj.
    internal static class Mensajes
    {
        public static string PlantillaNoEncontrada =>
            ResourceManager.GetString(nameof(PlantillaNoEncontrada))!;
    }
}
