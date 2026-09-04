using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// ADR-0012: mensajes de error del evento TurnoCreado en archivo .resx separado
// internal: accesible desde tests via InternalsVisibleTo en el .csproj
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
