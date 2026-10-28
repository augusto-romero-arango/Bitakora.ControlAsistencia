using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.SincronizarTurnoDePlantillaTurnosFunction.CommandHandler;

public partial class SincronizarTurnoDePlantillaTurnosCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.SincronizarTurnoDePlantillaTurnosFunction.CommandHandler.SincronizarTurnoDePlantillaTurnosCommandHandlerMensajes",
        typeof(SincronizarTurnoDePlantillaTurnosCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string PlantillaNoEncontrada =>
            ResourceManager.GetString(nameof(PlantillaNoEncontrada))!;
    }
}
