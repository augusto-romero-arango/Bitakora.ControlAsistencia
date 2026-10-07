using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class HorasYMinutos
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.DomainEvents.HorasYMinutosMensajes", typeof(HorasYMinutos).Assembly);
    public static class Mensajes
    {
        public static string HorasNegativas => ResourceManager.GetString(nameof(HorasNegativas))!;
        public static string MinutosFueraDeRango => ResourceManager.GetString(nameof(MinutosFueraDeRango))!;
        public static string Horas => ResourceManager.GetString(nameof(Horas))!;
        public static string Minutos => ResourceManager.GetString(nameof(Minutos))!;
    }
}
