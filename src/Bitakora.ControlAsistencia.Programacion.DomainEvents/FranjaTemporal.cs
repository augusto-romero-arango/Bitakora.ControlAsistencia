using System.Reflection;
using System.Resources;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public abstract class FranjaTemporal
{
    private const int MinutosPorHora = 60;

    // protected: FranjaOrdinaria la usa para su propio tope de 24 horas.
    protected const int MinutosPorDia = 1440;

    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.DomainEvents.FranjaTemporalMensajes",
        typeof(FranjaTemporal).Assembly);

    public static class Mensajes
    {
        public static string DuracionNoPositiva =>
            ResourceManager.GetString(nameof(DuracionNoPositiva))!;

        public static string FranjaHijaFueraDeContenedor =>
            ResourceManager.GetString(nameof(FranjaHijaFueraDeContenedor))!;

        public static string FranjasHijasSeSuperponen =>
            ResourceManager.GetString(nameof(FranjasHijasSeSuperponen))!;

        public static string LabelDescanso =>
            ResourceManager.GetString(nameof(LabelDescanso))!;

        public static string LabelExtra =>
            ResourceManager.GetString(nameof(LabelExtra))!;
    }

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
    private int MinutosAbsolutoInicio => CalcularMinutosAbsolutos(_horaInicio, _diaOffsetInicio);
    private int MinutosAbsolutoFin => CalcularMinutosAbsolutos(_horaFin, _diaOffsetFin);

    public int DuracionEnMinutos() => MinutosAbsolutoFin - MinutosAbsolutoInicio;

    public decimal DuracionEnHorasDecimales() => DuracionEnMinutos() / (decimal)MinutosPorHora;

    // EstaContenidaEn y SeSolapaCon son internal y no public: expresan la regla de FranjaOrdinaria sobre
    // sus hijas, no la superficie publica del evento. Extremos coincidentes SI cuentan como contenida.
    internal bool EstaContenidaEn(FranjaTemporal contenedor) =>
        MinutosAbsolutoInicio >= contenedor.MinutosAbsolutoInicio
        && MinutosAbsolutoFin <= contenedor.MinutosAbsolutoFin;

    // Fin exclusivo: dos franjas contiguas NO se solapan. Simetrico.
    internal bool SeSolapaCon(FranjaTemporal otra) =>
        MinutosAbsolutoInicio < otra.MinutosAbsolutoFin
        && otra.MinutosAbsolutoInicio < MinutosAbsolutoFin;

    // Publica: FranjaOrdinaria y SubFranja la usan desde el ensamblado del Function App sin abrir
    // _horaInicio (MEF-ADR-0012).
    public bool EmpiezaA(TimeOnly horaInicio) => _horaInicio == horaInicio;

    // Formato legible con offset: "06:00-12:00" o "22:00-06:00+1"
    public abstract override string ToString();

    protected static string FormatearHora(TimeOnly hora, int offset) =>
        offset == 0 ? hora.ToString("HH:mm") : $"{hora:HH:mm}+{offset}";

    private static int CalcularMinutosAbsolutos(TimeOnly hora, int diaOffset) =>
        hora.Hour * MinutosPorHora + hora.Minute + diaOffset * MinutosPorDia;

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
