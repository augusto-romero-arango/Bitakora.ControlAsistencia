using System.Resources;

namespace Bitakora.ControlAsistencia.ControlHoras.EstamparSedeCuandoSedeDeMarcacionResuelta.EventHandler;

// Mensajes del handler en .resx separado (MEF-ADR-0009).
// public y no internal: ControlHoras (Function App) no declara InternalsVisibleTo hacia sus tests,
// que afirman estos mensajes.
public partial class SedeDeMarcacionResueltaEventHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.ControlHoras.EstamparSedeCuandoSedeDeMarcacionResuelta.EventHandler.SedeDeMarcacionResueltaEventHandlerMensajes",
        typeof(SedeDeMarcacionResueltaEventHandler).Assembly);

    public static class Mensajes
    {
        public static string ControlDiarioNoEncontrado =>
            ResourceManager.GetString(nameof(ControlDiarioNoEncontrado))!;

        public static string MarcacionNoEncontrada =>
            ResourceManager.GetString(nameof(MarcacionNoEncontrada))!;
    }
}
