using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

// Issue #112: Rango temporal entre dos MomentoDelDia.
// Tiene invariante Inicio < Fin => sealed partial class con factory static (ADR-0015).
// Issue #143: Alinear con ADR-0015 - campos readonly privados + ConfigurarSerializacion,
// eliminando [JsonConstructor] que no funciona con Marten (ver ADR-0015 lineas 227-230).
// PR #148 (review): aplicar Tell-don't-Ask. Los momentos de inicio y fin viven como
// detalle interno; la API publica expone solo comportamiento (DuracionEnMinutos,
// ResolverA, ToString, Partir, igualdad por valor).
public sealed partial class IntervaloTemporal : IEquatable<IntervaloTemporal>
{
    private const int MinutosPorHora = 60;

    private readonly MomentoDelDia _inicio = null!;
    private readonly MomentoDelDia _fin = null!;

    // Ctor privado parametrizado: usado por los factories.
    // Sin [JsonConstructor] - Marten no respeta ese atributo en ctors privados (ADR-0015).
    private IntervaloTemporal(MomentoDelDia inicio, MomentoDelDia fin)
    {
        _inicio = inicio;
        _fin = fin;
    }

    // Ctor privado vacio: SOLO para STJ/Marten via typeInfo.CreateObject.
    // Nunca llamar desde el dominio - los campos quedan null hasta que ConfigurarSerializacion
    // los asigne via FieldInfo.SetValue.
    private IntervaloTemporal() { }

    // Factory con invariante: Inicio < Fin; lanza ArgumentException si se viola.
    public static IntervaloTemporal Crear(MomentoDelDia inicio, MomentoDelDia fin)
    {
        if (inicio.CompareTo(fin) >= 0)
            throw new ArgumentException(Mensajes.InicioDebeSerMenorQueFin);
        return new IntervaloTemporal(inicio, fin);
    }

    // Issue #143: Construye desde DateTimes anclados a una fecha ancla.
    // Usa MomentoDelDia.Desde internamente para calcular DiaOffset.
    public static IntervaloTemporal Desde(DateTime inicio, DateTime fin, DateOnly fechaAncla) =>
        Crear(MomentoDelDia.Desde(inicio, fechaAncla), MomentoDelDia.Desde(fin, fechaAncla));

    // Issue #143: Parte el intervalo en un punto intermedio.
    // Rechaza minutosDesdeInicio <= 0 || minutosDesdeInicio >= DuracionEnMinutos.
    public (IntervaloTemporal izquierdo, IntervaloTemporal derecho) Partir(int minutosDesdeInicio)
    {
        if (minutosDesdeInicio <= 0 || minutosDesdeInicio >= DuracionEnMinutos)
            throw new ArgumentException(Mensajes.PuntoDeParticionDebeSerInterior);

        var puntoIntermedio = MomentoDelDia.DesdeMinutosAbsolutos(_inicio.MinutosAbsolutos + minutosDesdeInicio);
        return (Crear(_inicio, puntoIntermedio), Crear(puntoIntermedio, _fin));
    }

    public int DuracionEnMinutos => _fin.MinutosAbsolutos - _inicio.MinutosAbsolutos;

    public decimal DuracionEnHorasDecimales => DuracionEnMinutos / (decimal)MinutosPorHora;

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

            // Sin propiedades publicas Inicio/Fin que STJ pueda auto-detectar,
            // registramos manualmente las propiedades JSON sobre los campos privados.
            // El JSON producido conserva la forma {"Inicio":{...},"Fin":{...}}.
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
