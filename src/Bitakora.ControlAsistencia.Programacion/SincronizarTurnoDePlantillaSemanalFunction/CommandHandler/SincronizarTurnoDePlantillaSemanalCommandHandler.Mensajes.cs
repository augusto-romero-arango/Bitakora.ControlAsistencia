using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.SincronizarTurnoDePlantillaSemanalFunction.CommandHandler;

public partial class SincronizarTurnoDePlantillaSemanalCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.SincronizarTurnoDePlantillaSemanalFunction.CommandHandler.SincronizarTurnoDePlantillaSemanalCommandHandlerMensajes",
        typeof(SincronizarTurnoDePlantillaSemanalCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string PlantillaNoEncontrada =>
            ResourceManager.GetString(nameof(PlantillaNoEncontrada))!;
    }
}
