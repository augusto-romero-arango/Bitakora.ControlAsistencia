namespace Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

// Issue #112: Value object que representa un punto en el tiempo con offset de dia.
// Sin invariantes en el constructor - record segun heuristica ADR-0015.
// DiaOffset 0 = mismo dia, 1 = dia siguiente.
public sealed record MomentoDelDia(TimeOnly Hora, int DiaOffset = 0)
    : IComparable<MomentoDelDia>
{
    private const int MinutosPorHora = 60;
    private const int MinutosPorDia = 1440;

    // Minutos absolutos desde el dia base: Hora.Hour * 60 + Hora.Minute + DiaOffset * 1440
    // Uso interno para aritmetica de rangos y comparacion.
    public int MinutosAbsolutos =>
        Hora.Hour * MinutosPorHora + Hora.Minute + DiaOffset * MinutosPorDia;

    // Resuelve a DateTime concreto: fecha.AddDays(DiaOffset).ToDateTime(Hora)
    public DateTime ResolverA(DateOnly fecha) =>
        fecha.AddDays(DiaOffset).ToDateTime(Hora);

    // "08:00" sin offset, "06:00+1" con offset
    public override string ToString() =>
        DiaOffset == 0
            ? Hora.ToString("HH:mm")
            : $"{Hora:HH:mm}+{DiaOffset}";

    // Compara por MinutosAbsolutos
    public int CompareTo(MomentoDelDia? other)
    {
        if (other is null) return 1;
        return MinutosAbsolutos.CompareTo(other.MinutosAbsolutos);
    }
}
