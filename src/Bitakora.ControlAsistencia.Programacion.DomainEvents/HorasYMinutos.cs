using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class HorasYMinutos : IComparable<HorasYMinutos>, IEquatable<HorasYMinutos>
{
    private const int MinutosPorHora = 60;
    private int _minutosTotales;

    private HorasYMinutos() { }
    private HorasYMinutos(int minutosTotales) => _minutosTotales = minutosTotales;

    public static HorasYMinutos Crear(int horas, int minutos)
    {
        var errores = new List<Exception>();
        if (horas < 0)
            errores.Add(new ArgumentException(Mensajes.HorasNegativas));
        if (minutos is < 0 or >= MinutosPorHora)
            errores.Add(new ArgumentException(Mensajes.MinutosFueraDeRango));
        if (errores.Count > 0)
            throw new AggregateException(errores);

        return new HorasYMinutos(checked(horas * MinutosPorHora + minutos));
    }

    public int TotalMinutos() => _minutosTotales;
    public int Horas => _minutosTotales / MinutosPorHora;
    public int Minutos => _minutosTotales % MinutosPorHora;

    public int CompareTo(HorasYMinutos? otro) => otro is null ? 1 : _minutosTotales.CompareTo(otro._minutosTotales);
    public bool Equals(HorasYMinutos? otro) => otro is not null && _minutosTotales == otro._minutosTotales;
    public override bool Equals(object? obj) => Equals(obj as HorasYMinutos);
    public override int GetHashCode() => _minutosTotales.GetHashCode();
    public HorasYMinutos Por(int factor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(factor);
        return new(checked(_minutosTotales * factor));
    }
    public override string ToString()
    {
        var horas = _minutosTotales / MinutosPorHora;
        var minutos = _minutosTotales % MinutosPorHora;
        if (minutos == 0)
            return $"{horas} {Mensajes.Horas}";
        if (horas == 0)
            return $"{minutos} {Mensajes.Minutos}";
        return $"{horas} {Mensajes.Horas} {minutos} {Mensajes.Minutos}";
    }

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var campo = typeof(HorasYMinutos).GetField(nameof(_minutosTotales), BindingFlags.NonPublic | BindingFlags.Instance)!;
        var ctor = typeof(HorasYMinutos).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != typeof(HorasYMinutos) || info.Kind != JsonTypeInfoKind.Object) return;
            info.CreateObject = () => (HorasYMinutos)ctor.Invoke(null);
            info.Properties.Clear();
            var propiedad = info.CreateJsonPropertyInfo(typeof(int), "MinutosTotales");
            propiedad.Get = obj => campo.GetValue(obj);
            propiedad.Set = (obj, valor) => campo.SetValue(obj, valor);
            info.Properties.Add(propiedad);
        });
    }
}
