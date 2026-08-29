using System.Resources;

namespace Bitakora.ControlAsistencia.ControlHoras.EstamparSedeCuandoSedeDeMarcacionResuelta.EventHandler;

// MEF-ADR-0009: mensajes del handler en .resx separado.
// public: este dominio no declara InternalsVisibleTo desde ControlHoras (Function App) hacia
// ControlHoras.Tests (a diferencia de ControlHoras.DomainEvents, que si lo declara) -- ver
// ComposicionServiciosTests, comentario de VariableRatioSampling.
public partial class SedeDeMarcacionResueltaEventHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.ControlHoras.EstamparSedeCuandoSedeDeMarcacionResuelta.EventHandler.SedeDeMarcacionResueltaEventHandlerMensajes",
        typeof(SedeDeMarcacionResueltaEventHandler).Assembly);

    public static class Mensajes
    {
        // CA-3: el ControlDiario destino no existe todavia -- el retry del Service Bus lo resuelve
        // cuando AdicionarMarcacion ya lo haya creado.
        public static string ControlDiarioNoEncontrado =>
            ResourceManager.GetString(nameof(ControlDiarioNoEncontrado))!;

        // CA-3: el ControlDiario existe pero la marcacion (TimestampNormalizado+DispositivoId) aun
        // no fue adicionada -- misma carrera de orden, mismo retry del Service Bus.
        public static string MarcacionNoEncontrada =>
            ResourceManager.GetString(nameof(MarcacionNoEncontrada))!;
    }
}
