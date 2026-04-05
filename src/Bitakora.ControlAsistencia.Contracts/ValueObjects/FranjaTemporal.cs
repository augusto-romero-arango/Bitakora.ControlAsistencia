using System.Resources;

namespace Bitakora.ControlAsistencia.Contracts.ValueObjects;

// Clase abstracta base para todas las franjas temporales de un turno.
// Encapsula HoraInicio, HoraFin, calculo de duracion y ToString().
// ADR-0015: record con factory estatico, constructor privado.
public abstract record FranjaTemporal
{
    protected const int MinutosPorHora = 60;
    protected const int MinutosPorDia = 1440;

    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Contracts.ValueObjects.FranjaTemporalMensajes",
        typeof(FranjaTemporal).Assembly);

    public static class Mensajes
    {
        public static string InicioYFinIguales =>
            ResourceManager.GetString(nameof(InicioYFinIguales))!;

        public static string FranjaHijaFueraDeContenedor =>
            ResourceManager.GetString(nameof(FranjaHijaFueraDeContenedor))!;

        public static string FranjasHijasSeSuperponen =>
            ResourceManager.GetString(nameof(FranjasHijasSeSuperponen))!;

        public static string LabelDescansos =>
            ResourceManager.GetString(nameof(LabelDescansos)) ?? "Descansos";

        public static string LabelExtras =>
            ResourceManager.GetString(nameof(LabelExtras)) ?? "Extras";
    }

    protected FranjaTemporal() { }

    protected TimeOnly HoraInicio { get; init; }
    protected TimeOnly HoraFin { get; init; }

    // Minutos absolutos desde el dia base, considerando offset
    // internal para que FranjaOrdinaria pueda validar contencion y solapamiento de hijas
    internal abstract int MinutosAbsolutoInicio { get; }
    internal abstract int MinutosAbsolutoFin { get; }

    // CA-10, CA-11: calculo de duracion en minutos considerando offsets
    public int DuracionEnMinutos() => MinutosAbsolutoFin - MinutosAbsolutoInicio;

    // CA-12: conversor a horas decimales
    public double DuracionEnHorasDecimales() => DuracionEnMinutos() / (double)MinutosPorHora;

    // CA-20: formato legible - "(06:00-12:00)" o "(22:00-06:00+1)" con offset
    public abstract override string ToString();

    protected static string FormatearHora(TimeOnly hora, int offset) =>
        offset == 0 ? hora.ToString("HH:mm") : $"{hora:HH:mm}+{offset}";

    protected static int CalcularMinutosAbsolutos(TimeOnly hora, int diaOffset) =>
        hora.Hour * MinutosPorHora + hora.Minute + diaOffset * MinutosPorDia;
}
