using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class TurnoCreado
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.DomainEvents.TurnoCreadoMensajes",
        typeof(TurnoCreado).Assembly);

    internal static class Mensajes
    {
        public static string NombreVacio =>
            ResourceManager.GetString(nameof(NombreVacio))!;

        public static string FranjasOrdinariasSeSolapan =>
            ResourceManager.GetString(nameof(FranjasOrdinariasSeSolapan))!;
    }
}
