using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

// Invariante Inicio < Fin. Los extremos son detalle interno: la API publica expone solo comportamiento.
public sealed partial class IntervaloTemporal : IEquatable<IntervaloTemporal>, IComparable<IntervaloTemporal>
{
    private const int MinutosPorHora = 60;

    private readonly MomentoDelDia _inicio = null!;
    private readonly MomentoDelDia _fin = null!;

    // Sin [JsonConstructor]: Marten no respeta ese atributo en ctors privados.
    private IntervaloTemporal(MomentoDelDia inicio, MomentoDelDia fin)
    {
        _inicio = inicio;
        _fin = fin;
    }

    // Solo para STJ/Marten: los campos quedan null hasta que ConfigurarSerializacion los asigne.
    private IntervaloTemporal() { }

    public static IntervaloTemporal Crear(MomentoDelDia inicio, MomentoDelDia fin)
    {
        if (inicio.CompareTo(fin) >= 0)
            throw new ArgumentException(Mensajes.InicioDebeSerMenorQueFin);
        return new IntervaloTemporal(inicio, fin);
    }

    public static IntervaloTemporal Desde(DateTime inicio, DateTime fin, DateOnly fechaAncla) =>
        Crear(MomentoDelDia.Desde(inicio, fechaAncla), MomentoDelDia.Desde(fin, fechaAncla));

    public (IntervaloTemporal izquierdo, IntervaloTemporal derecho) Partir(int minutosDesdeInicio)
    {
        if (minutosDesdeInicio <= 0 || minutosDesdeInicio >= DuracionEnMinutos)
            throw new ArgumentException(Mensajes.PuntoDeParticionDebeSerInterior);

        var puntoIntermedio = MomentoDelDia.DesdeMinutosAbsolutos(_inicio.MinutosAbsolutos + minutosDesdeInicio);
        return (Crear(_inicio, puntoIntermedio), Crear(puntoIntermedio, _fin));
    }

    public int DuracionEnMinutos => _fin.MinutosAbsolutos - _inicio.MinutosAbsolutos;

    public decimal DuracionEnHorasDecimales => DuracionEnMinutos / (decimal)MinutosPorHora;

    // Corta en cada ocurrencia diaria de las fronteras dadas, sin incluir los extremos.
    public IReadOnlyList<IntervaloTemporal> Segmentar(IEnumerable<TimeOnly> fronterasDiarias)
    {
        var inicioMin = _inicio.MinutosAbsolutos;
        var finMin = _fin.MinutosAbsolutos;

        var fronteras = Enumerable.Range(_inicio.DiaOffset, _fin.DiaOffset - _inicio.DiaOffset + 1)
            .SelectMany(dia => fronterasDiarias.Select(t => new MomentoDelDia(t, dia).MinutosAbsolutos))
            .Where(f => f > inicioMin && f < finMin)
            .OrderBy(f => f)
            .ToList();

        if (fronteras.Count == 0) return [this];

        var resultado = new List<IntervaloTemporal>(fronteras.Count + 1);
        var actual = this;
        foreach (var frontera in fronteras)
        {
            var (izq, der) = actual.Partir(frontera - actual._inicio.MinutosAbsolutos);
            resultado.Add(izq);
            actual = der;
        }
        resultado.Add(actual);
        return resultado;
    }

    // Resuelve ambos extremos a DateTime usando la fecha como ancla del DiaOffset.
    public (DateTime Inicio, DateTime Fin) ResolverA(DateOnly fecha) =>
        (_inicio.ResolverA(fecha), _fin.ResolverA(fecha));

    // "08:00-17:00 (540min)" o "22:00-06:00+1 (480min)"
    public override string ToString() =>
        $"{_inicio}-{_fin} ({DuracionEnMinutos}min)";

    public bool Equals(IntervaloTemporal? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return _inicio.Equals(other._inicio) && _fin.Equals(other._fin);
    }

    // Orden cronologico por inicio y, a igualdad de inicio, por fin.
    public int CompareTo(IntervaloTemporal? other)
    {
        if (other is null) return 1;
        var porInicio = _inicio.CompareTo(other._inicio);
        return porInicio != 0 ? porInicio : _fin.CompareTo(other._fin);
    }

    public override bool Equals(object? obj) => Equals(obj as IntervaloTemporal);

    public override int GetHashCode() => HashCode.Combine(_inicio, _fin);

    // Issue #143: Registro en el resolver de Marten (ADR-0015).
    // Debe llamarse desde ConfiguracionSerializacionControlHoras.ConfigurarResolver.
    // Sin este registro, Marten no puede deserializar IntervaloTemporal (ctor privado + readonly fields).
    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var fInicio = typeof(IntervaloTemporal)
            .GetField("_inicio", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var fFin = typeof(IntervaloTemporal)
            .GetField("_fin", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var ctor = typeof(IntervaloTemporal)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(IntervaloTemporal)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (IntervaloTemporal)ctor.Invoke(null);

            // Sin propiedades publicas que STJ detecte: se registran a mano sobre los campos
            // privados, con la forma JSON {"Inicio":{...},"Fin":{...}}.
            var pInicio = typeInfo.CreateJsonPropertyInfo(typeof(MomentoDelDia), "Inicio");
            pInicio.Get = obj => fInicio.GetValue(obj)!;
            pInicio.Set = (obj, val) => fInicio.SetValue(obj, val);
            typeInfo.Properties.Add(pInicio);

            var pFin = typeInfo.CreateJsonPropertyInfo(typeof(MomentoDelDia), "Fin");
            pFin.Get = obj => fFin.GetValue(obj)!;
            pFin.Set = (obj, val) => fFin.SetValue(obj, val);
            typeInfo.Properties.Add(pFin);
        });
    }
}
