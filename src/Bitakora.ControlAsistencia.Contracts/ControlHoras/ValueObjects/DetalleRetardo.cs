using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

// Issue #114: Detalle del retardo de una franja - intervalos retardados y compensados.
// ADR-0015: sealed class con factory (invariante) + ctor vacio privado + ConfigurarSerializacion.
// IEquatable manual con SequenceEqual en las listas (record no cumpliria la promesa).
public sealed partial class DetalleRetardo : IEquatable<DetalleRetardo>
{
    private readonly IReadOnlyList<IntervaloTemporal> _tiempoRetardado = [];
    private readonly IReadOnlyList<IntervaloTemporal> _tiempoCompensado = [];
    private readonly int _minutosRetardados;
    private readonly int _minutosCompensados;

    public IReadOnlyList<IntervaloTemporal> TiempoRetardado => _tiempoRetardado;
    public IReadOnlyList<IntervaloTemporal> TiempoCompensado => _tiempoCompensado;
    public int MinutosRetardados => _minutosRetardados;
    public int MinutosCompensados => _minutosCompensados;
    public int RetardoNeto => _minutosRetardados - _minutosCompensados;

    private DetalleRetardo(
        IReadOnlyList<IntervaloTemporal> tiempoRetardado,
        IReadOnlyList<IntervaloTemporal> tiempoCompensado,
        int minutosRetardados,
        int minutosCompensados)
    {
        _tiempoRetardado = tiempoRetardado;
        _tiempoCompensado = tiempoCompensado;
        _minutosRetardados = minutosRetardados;
        _minutosCompensados = minutosCompensados;
    }

    private DetalleRetardo() { }

    public static DetalleRetardo Crear(
        IReadOnlyList<IntervaloTemporal> tiempoRetardado,
        IReadOnlyList<IntervaloTemporal> tiempoCompensado)
    {
        var minutosRetardados = tiempoRetardado.Sum(i => i.DuracionEnMinutos);
        var minutosCompensados = tiempoCompensado.Sum(i => i.DuracionEnMinutos);
        if (minutosCompensados > minutosRetardados)
            throw new ArgumentException(Mensajes.CompensadosExcedenRetardados);
        return new DetalleRetardo(tiempoRetardado, tiempoCompensado, minutosRetardados, minutosCompensados);
    }

    public static readonly DetalleRetardo Vacio = new([], [], 0, 0);

    public bool Equals(DetalleRetardo? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return _tiempoRetardado.SequenceEqual(other._tiempoRetardado)
            && _tiempoCompensado.SequenceEqual(other._tiempoCompensado)
            && _minutosRetardados == other._minutosRetardados
            && _minutosCompensados == other._minutosCompensados;
    }

    public override bool Equals(object? obj) => Equals(obj as DetalleRetardo);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var i in _tiempoRetardado) hash.Add(i);
        foreach (var i in _tiempoCompensado) hash.Add(i);
        hash.Add(_minutosRetardados);
        hash.Add(_minutosCompensados);
        return hash.ToHashCode();
    }

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var fTiempoRetardado = typeof(DetalleRetardo)
            .GetField("_tiempoRetardado", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var fTiempoCompensado = typeof(DetalleRetardo)
            .GetField("_tiempoCompensado", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var fMinutosRetardados = typeof(DetalleRetardo)
            .GetField("_minutosRetardados", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var fMinutosCompensados = typeof(DetalleRetardo)
            .GetField("_minutosCompensados", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var ctor = typeof(DetalleRetardo)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(DetalleRetardo)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (DetalleRetardo)ctor.Invoke(null);

            var pR = typeInfo.CreateJsonPropertyInfo(
                typeof(IReadOnlyList<IntervaloTemporal>), "tiempoRetardado");
            pR.Get = obj => fTiempoRetardado.GetValue(obj)!;
            pR.Set = (obj, val) => fTiempoRetardado.SetValue(obj, val);
            typeInfo.Properties.Add(pR);

            var pC = typeInfo.CreateJsonPropertyInfo(
                typeof(IReadOnlyList<IntervaloTemporal>), "tiempoCompensado");
            pC.Get = obj => fTiempoCompensado.GetValue(obj)!;
            pC.Set = (obj, val) => fTiempoCompensado.SetValue(obj, val);
            typeInfo.Properties.Add(pC);

            var pMR = typeInfo.CreateJsonPropertyInfo(typeof(int), "minutosRetardados");
            pMR.Get = obj => fMinutosRetardados.GetValue(obj)!;
            pMR.Set = (obj, val) => fMinutosRetardados.SetValue(obj, val);
            typeInfo.Properties.Add(pMR);

            var pMC = typeInfo.CreateJsonPropertyInfo(typeof(int), "minutosCompensados");
            pMC.Get = obj => fMinutosCompensados.GetValue(obj)!;
            pMC.Set = (obj, val) => fMinutosCompensados.SetValue(obj, val);
            typeInfo.Properties.Add(pMC);
        });
    }
}
