using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.RetirarPlantillaTurnosFunction.CommandHandler;

public partial class RetirarPlantillaTurnosCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.RetirarPlantillaTurnosFunction.CommandHandler.RetirarPlantillaTurnosCommandHandlerMensajes",
        typeof(RetirarPlantillaTurnosCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string PlantillaNoEncontrada =>
            ResourceManager.GetString(nameof(PlantillaNoEncontrada))!;
    }
}
