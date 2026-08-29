using System.Resources;

namespace Bitakora.ControlAsistencia.Sedes.ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado.EventHandler;

public partial class RegistroDeMarcacionCreadoEventHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Sedes.ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado.EventHandler.RegistroDeMarcacionCreadoEventHandlerMensajes",
        typeof(RegistroDeMarcacionCreadoEventHandler).Assembly);

    // internal, no private: los tests lo afirman via InternalsVisibleTo (ya declarado en el csproj).
    internal static class Mensajes
    {
        public static string DispositivoDesconocidoMarcando =>
            ResourceManager.GetString(nameof(DispositivoDesconocidoMarcando))!;
    }
}
