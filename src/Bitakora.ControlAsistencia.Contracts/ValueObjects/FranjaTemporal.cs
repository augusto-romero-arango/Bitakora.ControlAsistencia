using System.Reflection;
using System.Resources;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Contracts.ValueObjects;

// Clase abstracta base para todas las franjas temporales de un turno.
// Encapsula estado interno y expone solo comportamiento.
// ADR-0015: sealed class con factory static, constructor privado, campos readonly.
public abstract class FranjaTemporal
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
            ResourceManager.GetString(nameof(LabelDescansos))!;

        public static string LabelExtras =>
            ResourceManager.GetString(nameof(LabelExtras))!;
    }

    // Estado interno - solo las subclases lo ven para calculos
    protected readonly TimeOnly _horaInicio;
    protected readonly TimeOnly _horaFin;

    protected FranjaTemporal(TimeOnly horaInicio, TimeOnly horaFin)
    {
        _horaInicio = horaInicio;
        _horaFin = horaFin;
    }

    // Constructor vacio para STJ/Marten
    protected FranjaTemporal()
    {
        _horaInicio = default;
        _horaFin = default;
    }

    // Minutos absolutos desde el dia base, considerando offset
    // internal para que FranjaOrdinaria valide contencion y solapamiento de hijas
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

    // Helper para que las subclases registren campos comunes de serializacion
    protected static void RegistrarCampo(
        JsonTypeInfo typeInfo, string nombreCampo, string nombreJson, Type tipoCampo, Type tipoClase)
    {
        var field = tipoClase.GetField(nombreCampo, BindingFlags.NonPublic | BindingFlags.Instance)!;
        var prop = typeInfo.CreateJsonPropertyInfo(tipoCampo, nombreJson);
        prop.Get = obj => field.GetValue(obj)!;
        prop.Set = (obj, val) => field.SetValue(obj, val);
        typeInfo.Properties.Add(prop);
    }
}
