using System.Resources;

namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

public sealed partial class MarcacionRegistrada
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.ControlHoras.DomainEvents.MarcacionRegistradaMensajes",
        typeof(MarcacionRegistrada).Assembly);

    internal static class Mensajes
    {
        public static string CodigoColaboradorVacio =>
            ResourceManager.GetString(nameof(CodigoColaboradorVacio))!;
    }
}
