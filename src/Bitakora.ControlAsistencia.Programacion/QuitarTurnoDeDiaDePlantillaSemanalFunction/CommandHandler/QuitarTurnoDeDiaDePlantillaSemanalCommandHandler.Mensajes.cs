using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.QuitarTurnoDeDiaDePlantillaSemanalFunction.CommandHandler;

public partial class QuitarTurnoDeDiaDePlantillaSemanalCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.QuitarTurnoDeDiaDePlantillaSemanalFunction.CommandHandler.QuitarTurnoDeDiaDePlantillaSemanalCommandHandlerMensajes",
        typeof(QuitarTurnoDeDiaDePlantillaSemanalCommandHandler).Assembly);

    // internal, no private: los tests los leen via InternalsVisibleTo del .csproj.
    internal static class Mensajes
    {
        public static string PlantillaNoEncontrada =>
            ResourceManager.GetString(nameof(PlantillaNoEncontrada))!;

        public static string SemanaFueraDeRango =>
            ResourceManager.GetString(nameof(SemanaFueraDeRango))!;
    }
}
