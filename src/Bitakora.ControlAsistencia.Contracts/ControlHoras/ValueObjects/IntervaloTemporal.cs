using System.Text.Json.Serialization;

namespace Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

// Issue #112: Rango temporal entre dos MomentoDelDia.
// Tiene invariante Inicio < Fin => sealed partial class con factory static (ADR-0015).
public sealed partial class IntervaloTemporal : IEquatable<IntervaloTemporal>
{
    private const int MinutosPorHora = 60;

    public MomentoDelDia Inicio { get; }
    public MomentoDelDia Fin { get; }

    // STJ usa este ctor privado al deserializar (es el unico parametrizado).
    // No revalidamos la invariante aqui: asumimos que los bytes provienen de
    // un IntervaloTemporal ya validado via Crear().
    [JsonConstructor]
    private IntervaloTemporal(MomentoDelDia inicio, MomentoDelDia fin)
    {
        Inicio = inicio;
        Fin = fin;
    }

    // Factory con invariante: Inicio < Fin; lanza ArgumentException si se viola.
    public static IntervaloTemporal Crear(MomentoDelDia inicio, MomentoDelDia fin)
    {
        if (inicio.CompareTo(fin) >= 0)
            throw new ArgumentException(Mensajes.InicioDebeSerMenorQueFin);
        return new IntervaloTemporal(inicio, fin);
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
}
