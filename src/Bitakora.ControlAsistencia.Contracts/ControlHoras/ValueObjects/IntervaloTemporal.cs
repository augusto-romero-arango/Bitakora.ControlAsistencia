namespace Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

// Issue #112: Rango temporal entre dos MomentoDelDia.
// Tiene invariante Inicio < Fin => sealed partial class con factory static (ADR-0015).
public sealed partial class IntervaloTemporal : IEquatable<IntervaloTemporal>
{
    public MomentoDelDia Inicio { get; }
    public MomentoDelDia Fin { get; }

    private IntervaloTemporal(MomentoDelDia inicio, MomentoDelDia fin)
    {
        Inicio = inicio;
        Fin = fin;
    }

    // Constructor vacio para STJ/Marten
    private IntervaloTemporal()
    {
        Inicio = new MomentoDelDia(TimeOnly.MinValue);
        Fin = new MomentoDelDia(TimeOnly.MinValue);
    }

    // Factory con invariante: Inicio < Fin; lanza ArgumentException si se viola.
    public static IntervaloTemporal Crear(MomentoDelDia inicio, MomentoDelDia fin)
        => throw new NotImplementedException();

    // Fin.MinutosAbsolutos - Inicio.MinutosAbsolutos
    public int DuracionEnMinutos => throw new NotImplementedException();

    // DuracionEnMinutos / 60m - siempre decimal, nunca double
    public decimal DuracionEnHorasDecimales => throw new NotImplementedException();

    // Resuelve ambos extremos a DateTime
    public (DateTime Inicio, DateTime Fin) ResolverA(DateOnly fecha)
        => throw new NotImplementedException();

    // "08:00-17:00 (540min)" o "22:00-06:00+1 (480min)"
    public override string ToString() => throw new NotImplementedException();

    public bool Equals(IntervaloTemporal? other) => throw new NotImplementedException();
    public override bool Equals(object? obj) => Equals(obj as IntervaloTemporal);
    public override int GetHashCode() => throw new NotImplementedException();
}
