namespace Bitakora.ControlAsistencia.Contracts.ValueObjects;

/// <summary>
/// Pausa operativa dentro de una franja ordinaria (ej. almuerzo).
/// CA-2: Hereda de FranjaBase. Hoja - no contiene otras franjas.
/// CA-5: Tiene DiaOffsetInicio y DiaOffsetFin para franjas nocturnas.
/// </summary>
public record FranjaDescanso : FranjaBase
{
    /// <summary>CA-5: 0 para mismo dia, 1 para dia siguiente.</summary>
    public int DiaOffsetInicio { get; }

    /// <summary>CA-5: 0 para mismo dia, 1 para dia siguiente.</summary>
    public override int DiaOffsetFin { get; }

    public override int MinutosAbsolutoInicio =>
        HoraInicio.Hour * 60 + HoraInicio.Minute + DiaOffsetInicio * 1440;

    public override int MinutosAbsolutoFin =>
        HoraFin.Hour * 60 + HoraFin.Minute + DiaOffsetFin * 1440;

    private FranjaDescanso(
        TimeOnly horaInicio,
        TimeOnly horaFin,
        int diaOffsetInicio,
        int diaOffsetFin)
        : base(horaInicio, horaFin)
    {
        DiaOffsetInicio = diaOffsetInicio;
        DiaOffsetFin = diaOffsetFin;
    }

    // CA-9: constructor vacio para compatibilidad con Marten/JSON
    private FranjaDescanso() : base() { }

    /// <summary>
    /// CA-8: Factory static con validacion de invariantes.
    /// CA-7: Rechaza HoraInicio == HoraFin cuando el offset tambien es igual (duracion cero).
    /// </summary>
    public static FranjaDescanso Crear(
        TimeOnly horaInicio,
        TimeOnly horaFin,
        int diaOffsetInicio = 0,
        int diaOffsetFin = 0)
    {
        if (horaInicio == horaFin && diaOffsetInicio == diaOffsetFin)
            throw new ArgumentException("La hora de inicio y fin no pueden ser iguales con el mismo offset de dia.");

        return new FranjaDescanso(horaInicio, horaFin, diaOffsetInicio, diaOffsetFin);
    }

    /// <summary>
    /// CA-13: Crea el descanso infiriendo automaticamente el DiaOffsetFin
    /// relativo al DiaOffsetInicio dado. Si horaFin &lt; horaInicio, diaOffsetFin = diaOffsetInicio + 1.
    /// </summary>
    public static FranjaDescanso CrearInfiriendoOffset(
        TimeOnly horaInicio,
        TimeOnly horaFin,
        int diaOffsetInicio = 0)
    {
        var diaOffsetFin = horaFin < horaInicio ? diaOffsetInicio + 1 : diaOffsetInicio;
        return Crear(horaInicio, horaFin, diaOffsetInicio, diaOffsetFin);
    }
}
