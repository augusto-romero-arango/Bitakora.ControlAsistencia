namespace Bitakora.ControlAsistencia.ReadModels.ControlHoras;

/// <summary>
/// Vista por colaborador y semana ISO (lunes a domingo) que compara lo programado contra la
/// Jornada estampada. Record plano sin partial (MEF-ADR-0035/0041); el comportamiento vive en
/// AdvertenciasProgramacionSemanalProjection, en el worker.
/// Id = "{CodigoColaborador}:{AnioIso}-W{NumeroSemana:00}".
/// </summary>
[MaterializadoPorProyeccion]
public sealed record AdvertenciasProgramacionSemanal(
    string Id,
    string CodigoColaborador,
    string NombreCompleto,
    int AnioIso,
    int NumeroSemana,
    DateOnly Lunes,
    DateOnly Domingo,
    JornadaAplicada? Jornada,
    int MinutosOrdinariosProgramados,
    int DiasSinProgramar,
    bool TieneAusencias,
    bool EsJuzgable,
    IReadOnlyList<CasillaDia> Casillas)
{
    public IReadOnlyList<AdvertenciaSemanal> AdvertenciasSemanales { get; init; } = [];
    public bool TieneAdvertencias { get; init; }
}

/// <summary>Jornada de la semana: id y los cuatro valores en minutos (sin nombre).</summary>
public sealed record JornadaAplicada(
    Guid JornadaId,
    int HorasSemanalesEnMinutos,
    int TopeDiarioEnMinutos,
    int MinimoDiarioEnMinutos,
    int DiasDescansoPorSemana);

public enum TipoCasilla
{
    Trabajo,
    Descanso,
    Ausencia,
    SinProgramar
}

/// <summary>
/// Casilla de un dia. AusenciaId, TurnoCubierto, InicioCobertura y LimiteCoberturaVispera son estado
/// interno, no se presentan. AusenciaId y TurnoCubierto restauran la casilla al cancelar la ausencia
/// vigente. LimiteCoberturaVispera es del dia, no del turno: sobrevive a toda asignacion, cancelacion
/// o ausencia, porque el write-side solo lo vuelve a emitir cuando cambia.
/// </summary>
public sealed record CasillaDia(
    DateOnly Fecha,
    TipoCasilla Tipo,
    string NombreTurno,
    int MinutosOrdinarios,
    string? MotivoAusencia = null,
    Guid? AusenciaId = null,
    TurnoCubiertoSemana? TurnoCubierto = null,
    DateTime? InicioCobertura = null,
    DateTime? LimiteCoberturaVispera = null,
    DateTime? CierreVentanaEntrada = null,
    bool TieneVentanaDeEntrada = false,
    DateTime? CierreTurnoVispera = null,
    bool EsHorarioAbierto = false)
{
    public IReadOnlyList<AdvertenciaDiaria> Advertencias { get; init; } = [];
}

public sealed record TurnoCubiertoSemana(string NombreTurno, int MinutosOrdinarios, bool EsDescanso, bool EsHorarioAbierto = false);
