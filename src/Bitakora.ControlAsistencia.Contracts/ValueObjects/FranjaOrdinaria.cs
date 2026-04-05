namespace Bitakora.ControlAsistencia.Contracts.ValueObjects;

/// <summary>
/// Segmento continuo de trabajo dentro de un turno.
/// CA-2: Hereda de FranjaBase.
/// CA-4: Tiene DiaOffsetFin para representar franjas nocturnas.
/// </summary>
public record FranjaOrdinaria : FranjaBase
{
    /// <summary>CA-4: 0 para mismo dia, 1 para dia siguiente.</summary>
    public int DiaOffsetFin { get; }

    public IReadOnlyList<FranjaDescanso> Descansos { get; }
    public IReadOnlyList<FranjaExtra> Extras { get; }

    private FranjaOrdinaria(
        TimeOnly horaInicio,
        TimeOnly horaFin,
        int diaOffsetFin,
        IReadOnlyList<FranjaDescanso> descansos,
        IReadOnlyList<FranjaExtra> extras)
        : base(horaInicio, horaFin)
    {
        DiaOffsetFin = diaOffsetFin;
        Descansos = descansos;
        Extras = extras;
    }

    // CA-9: constructor vacio privado para compatibilidad con Marten/JSON
    private FranjaOrdinaria() : base()
    {
        Descansos = [];
        Extras = [];
    }

    /// <summary>
    /// CA-8: Factory static con validacion de invariantes.
    /// CA-7: Rechaza HoraInicio == HoraFin (con mismo offset equivalente).
    /// </summary>
    public static FranjaOrdinaria Crear(
        TimeOnly horaInicio,
        TimeOnly horaFin,
        int diaOffsetFin = 0,
        IReadOnlyList<FranjaDescanso>? descansos = null,
        IReadOnlyList<FranjaExtra>? extras = null)
        => throw new NotImplementedException();

    /// <summary>
    /// CA-13: Crea la franja infiriendo automaticamente el DiaOffsetFin.
    /// Si horaFin &lt; horaInicio, infiere diaOffsetFin = 1.
    /// </summary>
    public static FranjaOrdinaria CrearInfiriendoOffset(
        TimeOnly horaInicio,
        TimeOnly horaFin,
        IReadOnlyList<FranjaDescanso>? descansos = null,
        IReadOnlyList<FranjaExtra>? extras = null)
        => throw new NotImplementedException();

    /// <summary>
    /// CA-10 / CA-11: Duracion considerando DiaOffsetFin.
    /// </summary>
    public override int DuracionEnMinutos() => throw new NotImplementedException();

    /// <summary>CA-12: Duracion en horas decimales.</summary>
    public override double DuracionEnHorasDecimales() => throw new NotImplementedException();

    /// <summary>
    /// CA-14 / CA-15 / CA-16: Verifica si una franja hija esta contenida
    /// dentro del rango temporal de esta franja ordinaria, considerando offsets.
    /// </summary>
    public bool Contiene(FranjaBase franja) => throw new NotImplementedException();

    /// <summary>
    /// CA-17 / CA-18 / CA-19: Detecta si dos franjas se solapan.
    /// Fin exclusivo: franjas contiguas NO se consideran solapadas.
    /// </summary>
    public static bool SeSolapan(FranjaBase a, FranjaBase b) => throw new NotImplementedException();
}
