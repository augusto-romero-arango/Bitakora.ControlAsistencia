using System.Resources;

namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

public sealed partial class Identificacion
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Colaboradores.DomainEvents.IdentificacionMensajes",
        typeof(Identificacion).Assembly);

    public static class Mensajes
    {
        public static string NumeroVacio =>
            ResourceManager.GetString(nameof(NumeroVacio))!;
    }
}
