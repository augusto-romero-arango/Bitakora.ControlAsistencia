using System.Reflection;
using System.Resources;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Clase abstracta base para todas las franjas temporales de un turno.
// Encapsula estado interno y expone solo comportamiento.
// ADR-0015: sealed class con factory static, constructor privado, campos readonly.
public abstract class FranjaTemporal
{
    private const int MinutosPorHora = 60;
    private const int MinutosPorDia = 1440;

    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.DomainEvents.FranjaTemporalMensajes",
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
    protected readonly int _diaOffsetInicio;
    protected readonly int _diaOffsetFin;

    protected FranjaTemporal(TimeOnly horaInicio, TimeOnly horaFin,
        int diaOffsetInicio, int diaOffsetFin)
    {
        _horaInicio = horaInicio;
        _horaFin = horaFin;
        _diaOffsetInicio = diaOffsetInicio;
        _diaOffsetFin = diaOffsetFin;
    }

    // Constructor vacio para STJ/Marten
    protected FranjaTemporal() { }

    // Minutos absolutos desde el dia base, considerando offset.
    // Dato intermedio de la aritmetica temporal: nadie fuera de esta clase lo lee (MEF-ADR-0012).
    private int MinutosAbsolutoInicio => CalcularMinutosAbsolutos(_horaInicio, _diaOffsetInicio);
    private int MinutosAbsolutoFin => CalcularMinutosAbsolutos(_horaFin, _diaOffsetFin);

    // CA-10, CA-11: calculo de duracion en minutos considerando offsets
    public int DuracionEnMinutos() => MinutosAbsolutoFin - MinutosAbsolutoInicio;

    // CA-12: conversor a horas decimales
    public decimal DuracionEnHorasDecimales() => DuracionEnMinutos() / (decimal)MinutosPorHora;

    // Issue #285: la franja responde estas dos preguntas en vez de exponer los insumos del
    // calculo (MEF-ADR-0012). internal y no public: expresan la regla de FranjaOrdinaria sobre
    // sus hijas, no la superficie publica del evento.

    // Extremos coincidentes SI cuentan como contenida (CA-14 a CA-16 de #3)
    internal bool EstaContenidaEn(FranjaTemporal contenedor) =>
        MinutosAbsolutoInicio >= contenedor.MinutosAbsolutoInicio
        && MinutosAbsolutoFin <= contenedor.MinutosAbsolutoFin;

    // Fin exclusivo: dos franjas contiguas NO se solapan (CA-18 de #3). Simetrico.
    internal bool SeSolapaCon(FranjaTemporal otra) =>
        MinutosAbsolutoInicio < otra.MinutosAbsolutoFin
        && otra.MinutosAbsolutoInicio < MinutosAbsolutoFin;

    // CA-20: formato legible - "(06:00-12:00)" o "(22:00-06:00+1)" con offset
    public abstract override string ToString();

    protected static string FormatearHora(TimeOnly hora, int offset) =>
        offset == 0 ? hora.ToString("HH:mm") : $"{hora:HH:mm}+{offset}";

    private static int CalcularMinutosAbsolutos(TimeOnly hora, int diaOffset) =>
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
