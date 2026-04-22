namespace Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

// Issue #112: Value object que representa un punto en el tiempo con offset de dia.
// Sin invariantes en el constructor - record segun heuristica ADR-0015.
// DiaOffset 0 = mismo dia, 1 = dia siguiente.
public sealed record MomentoDelDia(TimeOnly Hora, int DiaOffset = 0)
    : IComparable<MomentoDelDia>
{
    // Minutos absolutos desde el dia base: Hora.Hour * 60 + Hora.Minute + DiaOffset * 1440
    // Uso interno para aritmetica de rangos y comparacion.
    public int MinutosAbsolutos => throw new NotImplementedException();

    // Resuelve a DateTime concreto: fecha.AddDays(DiaOffset).ToDateTime(Hora)
    public DateTime ResolverA(DateOnly fecha) => throw new NotImplementedException();

    // "08:00" sin offset, "06:00+1" con offset
    public override string ToString() => throw new NotImplementedException();

    // Compara por MinutosAbsolutos
    public int CompareTo(MomentoDelDia? other) => throw new NotImplementedException();
}
