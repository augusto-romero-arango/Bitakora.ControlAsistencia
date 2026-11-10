namespace Bitakora.ControlAsistencia.ReadModels.Programacion;

/// <summary>
/// Ficha de un turno del catalogo: permite al Programador resolver Nombre -> TurnoId y confirmar si
/// el turno que necesita ya existe antes de crear uno nuevo.
/// </summary>
/// <remarks>
/// Id es el stream key del catalogo (TurnoId.ToString()), nunca un Guid.
///
/// EsDescanso viene del evento, no del conteo de franjas: un turno vacio no-descanso tambien tiene
/// cero franjas (CA-ADR-0033).
///
/// Completo (EsDescanso || Franjas.Count > 0) dice si el turno ya es programable; es derivado de
/// lectura, no campo del aggregate.
/// </remarks>
[MaterializadoPorProyeccion]
public sealed record FichaTurno(
    string Id,
    string Nombre,
    bool EsDescanso,
    string HorarioResumido,
    IReadOnlyList<FranjaFicha> Franjas,
    string Descripcion,
    bool Completo,
    VentanaDeEntradaFicha? VentanaDeEntrada = null,
    HorarioAbiertoFicha? HorarioAbierto = null);

/// <summary>
/// Ventana de entrada de un turno no fijo: entre Apertura y Cierre el colaborador puede entrar.
/// Amplitud es texto legible ("3 h", "2 h 30 min"); null en la ficha = turno fijo.
/// </summary>
public sealed record VentanaDeEntradaFicha(TimeOnly Apertura, TimeOnly Cierre, string Amplitud);

/// <summary>
/// Marca de turno de horario abierto: Duracion es texto legible ("2 h", "6 h 30 min"); null en la
/// ficha = turno que no es de horario abierto.
/// </summary>
public sealed record HorarioAbiertoFicha(string Duracion);

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
