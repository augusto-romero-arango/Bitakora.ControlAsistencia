using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.Eventos;

// ADR-0012: mensajes de error del evento TurnoCreado en archivo .resx separado
// internal: accesible desde tests via InternalsVisibleTo en el .csproj
public sealed partial class TurnoCreado
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.Eventos.TurnoCreadoMensajes",
        typeof(TurnoCreado).Assembly);

    internal static class Mensajes
    {
        public static string NombreVacio =>
            ResourceManager.GetString(nameof(NombreVacio))!;

        public static string SinFranjasOrdinarias =>
            ResourceManager.GetString(nameof(SinFranjasOrdinarias))!;

        public static string FranjasOrdinariasSeSolapan =>
            ResourceManager.GetString(nameof(FranjasOrdinariasSeSolapan))!;
    }
}
