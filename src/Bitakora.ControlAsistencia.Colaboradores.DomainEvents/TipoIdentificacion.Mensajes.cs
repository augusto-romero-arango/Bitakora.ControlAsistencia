using System.Resources;

namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

public sealed partial class TipoIdentificacion
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Colaboradores.DomainEvents.TipoIdentificacionMensajes",
        typeof(TipoIdentificacion).Assembly);

    public static class Mensajes
    {
        public static string CodigoNoReconocido =>
            ResourceManager.GetString(nameof(CodigoNoReconocido))!;
    }
}
