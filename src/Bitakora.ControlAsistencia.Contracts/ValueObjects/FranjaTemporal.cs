using System.Resources;

namespace Bitakora.ControlAsistencia.Contracts.ValueObjects;

// Clase abstracta base para todas las franjas temporales de un turno.
// Encapsula HoraInicio, HoraFin, calculo de duracion y ToString().
// ADR-0015: record con factory estatico, constructor privado.
public abstract record FranjaTemporal
{
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
    }

    // Constructor protegido para subclases - stub
    protected FranjaTemporal() { }

    public TimeOnly HoraInicio { get; protected init; }
    public TimeOnly HoraFin { get; protected init; }

    // CA-10, CA-11: calculo de duracion en minutos considerando offsets
    public int DuracionEnMinutos() => throw new NotImplementedException();

    // CA-12: conversor a horas decimales
    public double DuracionEnHorasDecimales() => throw new NotImplementedException();

    // CA-20: formato legible - "(06:00-12:00)" o "(22:00-06:00+1)" con offset
    public abstract override string ToString();
}
