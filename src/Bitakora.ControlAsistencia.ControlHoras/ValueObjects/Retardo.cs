using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

// Issue #114: Detalle del retardo de una franja - intervalos retardados y compensados.
// ADR-0015: sealed class con factory + ctor vacio privado + ConfigurarSerializacion.
// El unico observable publico es RetardoNeto (el castigo). Los datos crudos e intermedios
// son privados; se exponen como texto via ToString() para trazabilidad/auditoria.
public sealed partial class Retardo : IEquatable<Retardo>
{
    private readonly IReadOnlyList<IntervaloTemporal> _tiempoRetardado = [];
    private readonly IReadOnlyList<IntervaloTemporal> _tiempoCompensado = [];
    private readonly int _minutosRetardados;
    private readonly int _minutosCompensados;

    // Castigo efectivo: si la compensacion excede el retardo, el neto queda en cero.
    // El excedente se contabiliza en la liquidacion de extras, no aqui.
    public int RetardoNeto => Math.Max(0, _minutosRetardados - _minutosCompensados);

    private Retardo(
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

    private Retardo() { }

    public static Retardo Crear(
        IReadOnlyList<IntervaloTemporal> tiempoRetardado,
        IReadOnlyList<IntervaloTemporal> tiempoCompensado)
    {
        var minutosRetardados = tiempoRetardado.Sum(i => i.DuracionEnMinutos);
        var minutosCompensados = tiempoCompensado.Sum(i => i.DuracionEnMinutos);
        return new Retardo(tiempoRetardado, tiempoCompensado, minutosRetardados, minutosCompensados);
    }

    public static readonly Retardo Vacio = new([], [], 0, 0);

    // Issue #116: consolida los Retardo de las franjas de un dia en uno solo, sumando los
    // intervalos compensados cross-franja que calcula el ConsolidadorDesgloseHoras. El tiempo
    // retardado del dia es la concatenacion del retardado de cada franja; el compensado, la
    // concatenacion del compensado intra-franja mas el cross-franja. Mantiene encapsulados los
    // intervalos crudos de cada franja (ADR-0015): el consumidor entrega los Retardo
    // completos, no lee sus intervalos. Crear recalcula los minutos y respeta el invariante.
    public static Retardo Consolidar(
        IEnumerable<Retardo> retardosPorFranja,
        IReadOnlyList<IntervaloTemporal> compensacionCrossFranja)
    {
        var porFranja = retardosPorFranja.ToList();
        var tiempoRetardado = porFranja.SelectMany(r => r._tiempoRetardado).ToList();
        var tiempoCompensado = porFranja
            .SelectMany(r => r._tiempoCompensado)
            .Concat(compensacionCrossFranja)
            .ToList();
        return Crear(tiempoRetardado, tiempoCompensado);
    }

    public override string ToString()
    {
        if (_tiempoRetardado.Count == 0 && _tiempoCompensado.Count == 0)
            return Mensajes.SinRetardo;
        var retardos = string.Join(", ", _tiempoRetardado);
        var compensaciones = string.Join(", ", _tiempoCompensado);
        return $"{Mensajes.LabelRetardo}: [{retardos}] ({_minutosRetardados}min), " +
               $"{Mensajes.LabelCompensado}: [{compensaciones}] ({_minutosCompensados}min), " +
               $"{Mensajes.LabelNeto}: {RetardoNeto}min";
    }

    public bool Equals(Retardo? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return _tiempoRetardado.SequenceEqual(other._tiempoRetardado)
            && _tiempoCompensado.SequenceEqual(other._tiempoCompensado)
            && _minutosRetardados == other._minutosRetardados
            && _minutosCompensados == other._minutosCompensados;
    }

    public override bool Equals(object? obj) => Equals(obj as Retardo);

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
        var fTiempoRetardado = typeof(Retardo)
            .GetField("_tiempoRetardado", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var fTiempoCompensado = typeof(Retardo)
            .GetField("_tiempoCompensado", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var fMinutosRetardados = typeof(Retardo)
            .GetField("_minutosRetardados", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var fMinutosCompensados = typeof(Retardo)
            .GetField("_minutosCompensados", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var ctor = typeof(Retardo)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(Retardo)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (Retardo)ctor.Invoke(null);

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
