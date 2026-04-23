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
    public static IntervaloTemporal Desde(DateTime inicio, DateTime fin, DateOnly fechaAncla)
        => throw new NotImplementedException();

    // Issue #143: Parte el intervalo en un punto intermedio.
    // Rechaza minutosDesdeInicio <= 0 || minutosDesdeInicio >= DuracionEnMinutos.
    public (IntervaloTemporal izquierdo, IntervaloTemporal derecho) Partir(int minutosDesdeInicio)
        => throw new NotImplementedException();

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
    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
        => throw new NotImplementedException();
}
