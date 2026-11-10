namespace Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

// DiaOffset 0 = mismo dia, 1 = dia siguiente.
public sealed partial record MomentoDelDia(TimeOnly Hora, int DiaOffset = 0)
    : IComparable<MomentoDelDia>
{
    private const int MinutosPorHora = 60;
    private const int MinutosPorDia = 1440;

    public int MinutosAbsolutos =>
        Hora.Hour * MinutosPorHora + Hora.Minute + DiaOffset * MinutosPorDia;

    public DateTime ResolverA(DateOnly fecha) =>
        fecha.AddDays(DiaOffset).ToDateTime(Hora);

    // "08:00" sin offset, "06:00+1" con offset
    public override string ToString() =>
        DiaOffset == 0
            ? Hora.ToString("HH:mm")
            : $"{Hora:HH:mm}+{DiaOffset}";

    public int CompareTo(MomentoDelDia? other) =>
        other is null ? 1 : MinutosAbsolutos.CompareTo(other.MinutosAbsolutos);

    // Inverso de ResolverA(fecha).
    public static MomentoDelDia Desde(DateTime momento, DateOnly fechaAncla)
    {
        var diaOffset = (momento.Date - fechaAncla.ToDateTime(TimeOnly.MinValue).Date).Days;
        return new MomentoDelDia(TimeOnly.FromDateTime(momento), diaOffset);
    }

    public static MomentoDelDia DesdeMinutosAbsolutos(int minutosAbsolutos)
    {
        if (minutosAbsolutos < 0)
            throw new ArgumentException(Mensajes.MinutosAbsolutosDebeSerPositivoOCero);

        var diaOffset = minutosAbsolutos / MinutosPorDia;
        var minutosDelDia = minutosAbsolutos % MinutosPorDia;
        var hora = new TimeOnly(minutosDelDia / MinutosPorHora, minutosDelDia % MinutosPorHora);
        return new MomentoDelDia(hora, diaOffset);
    }
}
