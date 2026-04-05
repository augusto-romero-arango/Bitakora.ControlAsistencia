namespace Bitakora.ControlAsistencia.Contracts.ValueObjects;

// Horas extras programadas dentro de una franja ordinaria.
// Representan un compromiso del empleado.
// Es hoja - no contiene otras franjas.
// CA-5: tiene DiaOffsetInicio y DiaOffsetFin
// ADR-0015: record con factory estatico, constructor privado.
public sealed record FranjaExtra : FranjaTemporal
{
    // Constructor vacio privado para compatibilidad con Marten/JSON (CA-9)
    private FranjaExtra() { }

    // CA-5: offsets respecto al dia de la ordinaria padre
    public int DiaOffsetInicio { get; private init; }
    public int DiaOffsetFin { get; private init; }

    internal override int MinutosAbsolutoInicio => CalcularMinutosAbsolutos(HoraInicio, DiaOffsetInicio);
    internal override int MinutosAbsolutoFin => CalcularMinutosAbsolutos(HoraFin, DiaOffsetFin);

    // CA-8: factory estatico
    // CA-7: rechaza InicioYFinIguales
    public static FranjaExtra Crear(TimeOnly horaInicio, TimeOnly horaFin,
        int diaOffsetInicio = 0, int diaOffsetFin = 0)
    {
        if (horaInicio == horaFin && diaOffsetInicio == diaOffsetFin)
            throw new ArgumentException(Mensajes.InicioYFinIguales);

        return new FranjaExtra
        {
            HoraInicio = horaInicio,
            HoraFin = horaFin,
            DiaOffsetInicio = diaOffsetInicio,
            DiaOffsetFin = diaOffsetFin
        };
    }

    // CA-20, CA-21: formato heredado de la base
    public override string ToString() =>
        $"({FormatearHora(HoraInicio, DiaOffsetInicio)}-{FormatearHora(HoraFin, DiaOffsetFin)})";
}
