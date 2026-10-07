using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class LimitesJornada
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.DomainEvents.LimitesJornadaMensajes", typeof(LimitesJornada).Assembly);
    public static class Mensajes
    {
        public static string TopeDiarioFueraDeRango => ResourceManager.GetString(nameof(TopeDiarioFueraDeRango))!;
        public static string MinimoMayorQueTope => ResourceManager.GetString(nameof(MinimoMayorQueTope))!;
        public static string DescansosFueraDeRango => ResourceManager.GetString(nameof(DescansosFueraDeRango))!;
        public static string HorasSemanalesEnCero => ResourceManager.GetString(nameof(HorasSemanalesEnCero))!;
        public static string HorasSemanalesExcedenCapacidad => ResourceManager.GetString(nameof(HorasSemanalesExcedenCapacidad))!;
        public static string Semanales => ResourceManager.GetString(nameof(Semanales))!;
        public static string TopeDiario => ResourceManager.GetString(nameof(TopeDiario))!;
        public static string MinimoDiario => ResourceManager.GetString(nameof(MinimoDiario))!;
        public static string SinMinimoDiario => ResourceManager.GetString(nameof(SinMinimoDiario))!;
        public static string UnDiaDescanso => ResourceManager.GetString(nameof(UnDiaDescanso))!;
        public static string DiasDescanso => ResourceManager.GetString(nameof(DiasDescanso))!;
        public static string SinControlDescansos => ResourceManager.GetString(nameof(SinControlDescansos))!;
    }
}
