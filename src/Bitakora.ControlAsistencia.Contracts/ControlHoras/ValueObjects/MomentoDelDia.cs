namespace Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

// Issue #112: Value object que representa un punto en el tiempo con offset de dia.
// Sin invariantes en el constructor - record segun heuristica ADR-0015.
// DiaOffset 0 = mismo dia, 1 = dia siguiente.
// Issue #143: partial para soporte de Mensajes en archivo separado.
public sealed partial record MomentoDelDia(TimeOnly Hora, int DiaOffset = 0)
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

    // Compara por MinutosAbsolutos (null es menor por convencion de IComparable)
    public int CompareTo(MomentoDelDia? other) =>
        other is null ? 1 : MinutosAbsolutos.CompareTo(other.MinutosAbsolutos);

    // Issue #143: Inverso de ResolverA(fecha).
    // Calcula DiaOffset = (momento.Date - fechaAncla).Days y Hora = TimeOnly.FromDateTime(momento).
    public static MomentoDelDia Desde(DateTime momento, DateOnly fechaAncla)
    {
        var diaOffset = (momento.Date - fechaAncla.ToDateTime(TimeOnly.MinValue).Date).Days;
        return new MomentoDelDia(TimeOnly.FromDateTime(momento), diaOffset);
    }

    // Issue #143: Conversion numerica desde minutos absolutos.
    // DiaOffset = minutosAbsolutos / 1440, Hora desde minutosAbsolutos % 1440.
    // Rechaza minutosAbsolutos < 0 con ArgumentException.
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
