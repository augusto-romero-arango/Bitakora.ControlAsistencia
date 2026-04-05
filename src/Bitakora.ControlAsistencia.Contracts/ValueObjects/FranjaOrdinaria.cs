namespace Bitakora.ControlAsistencia.Contracts.ValueObjects;

/// <summary>
/// Segmento continuo de trabajo dentro de un turno.
/// CA-2: Hereda de FranjaBase.
/// CA-4: Tiene DiaOffsetFin para representar franjas nocturnas.
/// </summary>
public record FranjaOrdinaria : FranjaBase
{
    /// <summary>CA-4: 0 para mismo dia, 1 para dia siguiente.</summary>
    public override int DiaOffsetFin { get; }

    public IReadOnlyList<FranjaDescanso> Descansos { get; }
    public IReadOnlyList<FranjaExtra> Extras { get; }

    // FranjaOrdinaria siempre comienza en el dia base (offset 0).
    public override int MinutosAbsolutoInicio =>
        HoraInicio.Hour * 60 + HoraInicio.Minute;

    public override int MinutosAbsolutoFin =>
        HoraFin.Hour * 60 + HoraFin.Minute + DiaOffsetFin * 1440;

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

    // CA-9: constructor vacio para compatibilidad con Marten/JSON
    private FranjaOrdinaria() : base()
    {
        Descansos = [];
        Extras = [];
    }

    /// <summary>
    /// CA-8: Factory static con validacion de invariantes.
    /// CA-7: Rechaza HoraInicio == HoraFin — una franja ordinaria de 24h no es valida en este dominio.
    /// </summary>
    public static FranjaOrdinaria Crear(
        TimeOnly horaInicio,
        TimeOnly horaFin,
        int diaOffsetFin = 0,
        IReadOnlyList<FranjaDescanso>? descansos = null,
        IReadOnlyList<FranjaExtra>? extras = null)
    {
        if (horaInicio == horaFin)
            throw new ArgumentException("La hora de inicio y fin no pueden ser iguales.");

        return new FranjaOrdinaria(horaInicio, horaFin, diaOffsetFin,
            descansos ?? [],
            extras ?? []);
    }

    /// <summary>
    /// CA-13: Crea la franja infiriendo automaticamente el DiaOffsetFin.
    /// Si horaFin &lt; horaInicio, infiere diaOffsetFin = 1.
    /// </summary>
    public static FranjaOrdinaria CrearInfiriendoOffset(
        TimeOnly horaInicio,
        TimeOnly horaFin,
        IReadOnlyList<FranjaDescanso>? descansos = null,
        IReadOnlyList<FranjaExtra>? extras = null)
        => Crear(horaInicio, horaFin, InferirDiaOffset(horaInicio, horaFin), descansos, extras);

    /// <summary>
    /// CA-14 / CA-15 / CA-16: Verifica si una franja hija esta contenida
    /// dentro del rango temporal de esta franja ordinaria, considerando offsets.
    /// Inicio inclusivo: la franja puede comenzar en el mismo instante que la ordinaria.
    /// Fin inclusivo en contención: el fin de la hija puede igualar el fin de la padre.
    /// </summary>
    public bool Contiene(FranjaBase franja)
        => MinutosAbsolutoInicio <= franja.MinutosAbsolutoInicio
        && franja.MinutosAbsolutoFin <= MinutosAbsolutoFin;

    /// <summary>
    /// CA-17 / CA-18 / CA-19: Detecta si dos franjas se solapan.
    /// Fin exclusivo (CA-6): franjas contiguas NO se consideran solapadas.
    /// </summary>
    public static bool SeSolapan(FranjaBase a, FranjaBase b)
        => a.MinutosAbsolutoInicio < b.MinutosAbsolutoFin
        && b.MinutosAbsolutoInicio < a.MinutosAbsolutoFin;
}
