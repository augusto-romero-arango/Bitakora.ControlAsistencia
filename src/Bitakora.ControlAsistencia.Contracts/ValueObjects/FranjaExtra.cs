namespace Bitakora.ControlAsistencia.Contracts.ValueObjects;

/// <summary>
/// Horas extras programadas dentro de una franja ordinaria.
/// CA-2: Hereda de FranjaBase. Hoja - no contiene otras franjas.
/// CA-5: Tiene DiaOffsetInicio y DiaOffsetFin para franjas nocturnas.
/// </summary>
public record FranjaExtra : FranjaBase
{
    /// <summary>CA-5: 0 para mismo dia, 1 para dia siguiente.</summary>
    public int DiaOffsetInicio { get; }

    /// <summary>CA-5: 0 para mismo dia, 1 para dia siguiente.</summary>
    public int DiaOffsetFin { get; }

    private FranjaExtra(
        TimeOnly horaInicio,
        TimeOnly horaFin,
        int diaOffsetInicio,
        int diaOffsetFin)
        : base(horaInicio, horaFin)
    {
        DiaOffsetInicio = diaOffsetInicio;
        DiaOffsetFin = diaOffsetFin;
    }

    // CA-9: constructor vacio privado para compatibilidad con Marten/JSON
    private FranjaExtra() : base() { }

    /// <summary>
    /// CA-8: Factory static con validacion de invariantes.
    /// CA-7: Rechaza HoraInicio == HoraFin con mismo offset equivalente.
    /// </summary>
    public static FranjaExtra Crear(
        TimeOnly horaInicio,
        TimeOnly horaFin,
        int diaOffsetInicio = 0,
        int diaOffsetFin = 0)
        => throw new NotImplementedException();

    /// <summary>
    /// CA-13: Crea la extra infiriendo automaticamente el DiaOffsetFin
    /// relativo al DiaOffsetInicio dado. Si horaFin &lt; horaInicio, diaOffsetFin = diaOffsetInicio + 1.
    /// </summary>
    public static FranjaExtra CrearInfiriendoOffset(
        TimeOnly horaInicio,
        TimeOnly horaFin,
        int diaOffsetInicio = 0)
        => throw new NotImplementedException();

    /// <summary>CA-10 / CA-11: Duracion considerando DiaOffsetInicio y DiaOffsetFin.</summary>
    public override int DuracionEnMinutos() => throw new NotImplementedException();

    /// <summary>CA-12: Duracion en horas decimales.</summary>
    public override double DuracionEnHorasDecimales() => throw new NotImplementedException();
}
