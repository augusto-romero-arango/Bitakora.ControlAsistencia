using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class LimitesJornada : IEquatable<LimitesJornada>
{
    private const int DiasPorSemana = 7;
    private const int MaximoDescansos = DiasPorSemana - 1;
    private HorasYMinutos _horasSemanales = null!;
    private HorasYMinutos _topeDiario = null!;
    private HorasYMinutos _minimoDiario = null!;
    private int _diasDescansoPorSemana;

    private LimitesJornada() { }
    private LimitesJornada(HorasYMinutos horasSemanales, HorasYMinutos topeDiario,
        HorasYMinutos minimoDiario, int diasDescansoPorSemana)
    {
        _horasSemanales = horasSemanales;
        _topeDiario = topeDiario;
        _minimoDiario = minimoDiario;
        _diasDescansoPorSemana = diasDescansoPorSemana;
    }

    public static LimitesJornada Crear(HorasYMinutos horasSemanales, HorasYMinutos topeDiario,
        HorasYMinutos minimoDiario, int diasDescansoPorSemana)
    {
        var cero = HorasYMinutos.Crear(0, 0);
        var maximoDiario = HorasYMinutos.Crear(24, 0);
        var errores = new List<Exception>();
        if (topeDiario.CompareTo(cero) <= 0 || topeDiario.CompareTo(maximoDiario) > 0)
            errores.Add(new ArgumentException(Mensajes.TopeDiarioFueraDeRango));
        if (minimoDiario.CompareTo(topeDiario) > 0)
            errores.Add(new ArgumentException(Mensajes.MinimoMayorQueTope));
        if (diasDescansoPorSemana is < 0 or > MaximoDescansos)
            errores.Add(new ArgumentException(Mensajes.DescansosFueraDeRango));
        if (horasSemanales.CompareTo(cero) <= 0)
            errores.Add(new ArgumentException(Mensajes.HorasSemanalesEnCero));
        if (diasDescansoPorSemana is >= 0 and <= MaximoDescansos &&
            horasSemanales.CompareTo(topeDiario.Por(DiasPorSemana - diasDescansoPorSemana)) > 0)
            errores.Add(new ArgumentException(Mensajes.HorasSemanalesExcedenCapacidad));
        if (errores.Count > 0)
            throw new AggregateException(errores);

        return new LimitesJornada(horasSemanales, topeDiario, minimoDiario, diasDescansoPorSemana);
    }

    public HorasYMinutos HorasSemanales => _horasSemanales;
    public HorasYMinutos TopeDiario => _topeDiario;
    public HorasYMinutos MinimoDiario => _minimoDiario;
    public int DiasDescansoPorSemana => _diasDescansoPorSemana;

    public override string ToString()
    {
        var minimo = _minimoDiario.CompareTo(HorasYMinutos.Crear(0, 0)) == 0
            ? Mensajes.SinMinimoDiario : $"{Mensajes.MinimoDiario} {_minimoDiario}";
        var descansos = _diasDescansoPorSemana switch
        {
            0 => Mensajes.SinControlDescansos,
            1 => Mensajes.UnDiaDescanso,
            _ => $"{_diasDescansoPorSemana} {Mensajes.DiasDescanso}"
        };
        return $"{_horasSemanales} {Mensajes.Semanales}, {Mensajes.TopeDiario} {_topeDiario}, {minimo}, {descansos}";
    }

    public bool Equals(LimitesJornada? otro) => otro is not null &&
        _horasSemanales.Equals(otro._horasSemanales) &&
        _topeDiario.Equals(otro._topeDiario) &&
        _minimoDiario.Equals(otro._minimoDiario) &&
        _diasDescansoPorSemana == otro._diasDescansoPorSemana;
    public override bool Equals(object? obj) => Equals(obj as LimitesJornada);
    public override int GetHashCode() => HashCode.Combine(_horasSemanales, _topeDiario, _minimoDiario, _diasDescansoPorSemana);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var tipo = typeof(LimitesJornada);
        var ctor = tipo.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != tipo || info.Kind != JsonTypeInfoKind.Object) return;
            info.CreateObject = () => (LimitesJornada)ctor.Invoke(null);
            info.Properties.Clear();
            Agregar(nameof(_horasSemanales), "HorasSemanales", typeof(HorasYMinutos));
            Agregar(nameof(_topeDiario), "TopeDiario", typeof(HorasYMinutos));
            Agregar(nameof(_minimoDiario), "MinimoDiario", typeof(HorasYMinutos));
            Agregar(nameof(_diasDescansoPorSemana), "DiasDescansoPorSemana", typeof(int));

            void Agregar(string nombreCampo, string nombreJson, Type tipoCampo)
            {
                var campo = tipo.GetField(nombreCampo, BindingFlags.NonPublic | BindingFlags.Instance)!;
                var propiedad = info.CreateJsonPropertyInfo(tipoCampo, nombreJson);
                propiedad.Get = obj => campo.GetValue(obj);
                propiedad.Set = (obj, valor) => campo.SetValue(obj, valor);
                info.Properties.Add(propiedad);
            }
        });
    }
}
