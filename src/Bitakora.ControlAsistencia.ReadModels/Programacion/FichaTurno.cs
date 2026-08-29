namespace Bitakora.ControlAsistencia.ReadModels.Programacion;

/// <summary>
/// Ficha de un turno del catalogo: permite al Programador resolver Nombre -> TurnoId y confirmar si
/// el turno que necesita ya existe antes de crear uno nuevo.
/// </summary>
/// <remarks>
/// Record plano SIN partial ni comportamiento (MEF-ADR-0035): el mapeo evento -> vista
/// (HorarioResumido, Descripcion, EsDescanso) vive integro en la clase companion
/// FichaTurnoProjection, en el worker.
///
/// Vive en ReadModels, la cuarta isla del repo -- cero ProjectReference (MEF-ADR-0041 decision 2):
/// FranjaFicha/SubFranjaFicha son tipos propios, nunca los de Programacion.DomainEvents.
///
/// Id es el stream key del catalogo (TurnoId.ToString(), StreamIdentity.AsString), nunca un Guid.
///
/// EsDescanso no existe como campo del aggregate: se deriva de la variante del evento
/// (TurnoCreado.CrearDescanso, sin franjas).
/// </remarks>
public sealed record FichaTurno(
    string Id,
    string Nombre,
    bool EsDescanso,
    string HorarioResumido,
    IReadOnlyList<FranjaFicha> Franjas,
    string Descripcion);

/// <summary>
/// Franja completa de un turno del catalogo: el consumidor responde "hay descansos o extras dentro
/// de esta franja?" y "trae sede prearmada?" sin ir al event store.
/// </summary>
public sealed record FranjaFicha(
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int DiaOffsetFin,
    IReadOnlyList<SubFranjaFicha> Descansos,
    IReadOnlyList<SubFranjaFicha> Extras,
    string? SedeId,
    string? NombreSede,
    string Descripcion);

/// <summary>
/// Sub-franja (descanso o extra) contenida en una <see cref="FranjaFicha"/>.
/// </summary>
public sealed record SubFranjaFicha(
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int DiaOffsetInicio,
    int DiaOffsetFin);
