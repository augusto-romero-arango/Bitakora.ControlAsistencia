using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

// Issue #112: Rango temporal entre dos MomentoDelDia.
// Tiene invariante Inicio < Fin => sealed partial class con factory static (ADR-0015).
// Issue #143: Alinear con ADR-0015 - campos readonly privados + ConfigurarSerializacion,
// eliminando [JsonConstructor] que no funciona con Marten (ver ADR-0015 lineas 227-230).
public sealed partial class IntervaloTemporal : IEquatable<IntervaloTemporal>
{
    private const int MinutosPorHora = 60;

    private readonly MomentoDelDia _inicio = null!;
    private readonly MomentoDelDia _fin = null!;

    public MomentoDelDia Inicio => _inicio;
    public MomentoDelDia Fin => _fin;

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

        var puntoIntermedio = MomentoDelDia.DesdeMinutosAbsolutos(Inicio.MinutosAbsolutos + minutosDesdeInicio);
        return (Crear(Inicio, puntoIntermedio), Crear(puntoIntermedio, Fin));
    }

    // Fin.MinutosAbsolutos - Inicio.MinutosAbsolutos
    public int DuracionEnMinutos => Fin.MinutosAbsolutos - Inicio.MinutosAbsolutos;

    // DuracionEnMinutos / 60m - siempre decimal, nunca double
    public decimal DuracionEnHorasDecimales => DuracionEnMinutos / (decimal)MinutosPorHora;

    // Resuelve ambos extremos a DateTime
    public (DateTime Inicio, DateTime Fin) ResolverA(DateOnly fecha) =>
        (Inicio.ResolverA(fecha), Fin.ResolverA(fecha));

    // "08:00-17:00 (540min)" o "22:00-06:00+1 (480min)"
    public override string ToString() =>
        $"{Inicio}-{Fin} ({DuracionEnMinutos}min)";

    public bool Equals(IntervaloTemporal? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Inicio.Equals(other.Inicio) && Fin.Equals(other.Fin);
    }

    public override bool Equals(object? obj) => Equals(obj as IntervaloTemporal);

    public override int GetHashCode() => HashCode.Combine(Inicio, Fin);

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

            // Las propiedades publicas Inicio y Fin son get-only: STJ las auto-detecta
            // sin Set. Removerlas antes de registrar las versiones con Get+Set sobre los
            // campos privados readonly, de lo contrario el JSON tendria duplicados.
            for (var i = typeInfo.Properties.Count - 1; i >= 0; i--)
            {
                var nombre = typeInfo.Properties[i].Name;
                if (nombre is "Inicio" or "Fin")
                    typeInfo.Properties.RemoveAt(i);
            }

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
