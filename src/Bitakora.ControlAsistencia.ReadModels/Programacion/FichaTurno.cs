namespace Bitakora.ControlAsistencia.ReadModels.Programacion;

/// <summary>
/// Ficha de un turno del catalogo (issue #496): permite al Programador (front hoy, asistente MCP
/// en la vision de largo plazo) resolver Nombre -> TurnoId y confirmar si el turno que necesita ya
/// existe antes de crear uno nuevo. Sigue la familia acunada FichaColaborador/FichaSede -- la ficha
/// de un elemento de catalogo (MEF-ADR-0041).
/// </summary>
/// <remarks>
/// Record plano SIN partial (MEF-ADR-0035, skills/projections/modelos-marten.md): el comportamiento
/// de proyeccion vive en la clase companion FichaTurnoProjection, en el worker
/// (Bitakora.ControlAsistencia.Projections). Este tipo vive en ReadModels, la cuarta isla del repo
/// -- cero referencias de proyecto (ver el .csproj): FranjaFicha/SubFranjaFicha son propios, sin
/// relacion de tipo con Programacion.DomainEvents (MEF-ADR-0039 decision 2, enmendada por
/// MEF-ADR-0041 decision 1).
///
/// Sin sufijo "View" (MEF-ADR-0041 decision 3).
///
/// Id es el stream key del catalogo -- evento.TurnoId.ToString() (mismo criterio que
/// CatalogoTurnos.Apply, Events.StreamIdentity = AsString) -- nunca un Guid crudo.
///
/// EsDescanso se DERIVA de la variante del evento (TurnoCreado.CrearDescanso, franjas vacias): no
/// existe como campo propio del aggregate -- coherente con #423, la marca vive en la frontera de
/// lectura, no en el escritor.
///
/// HorarioResumido es la confirmacion RAPIDA ("el de 06:00-14:00?", espejo del patron de
/// TurnoVigente/#328) -- se compone en la projection, nunca aqui. Descripcion es la version
/// COMPLETA para desambiguar entre turnos de nombre parecido (incluye descansos/extras/sede de cada
/// franja); tambien se compone en la projection, nunca aqui (MEF-ADR-0041 decision 2).
/// </remarks>
public sealed record FichaTurno(
    string Id,
    string Nombre,
    bool EsDescanso,
    string HorarioResumido,
    IReadOnlyList<FranjaFicha> Franjas,
    string Descripcion);

/// <summary>
/// Franja completa de un turno del catalogo -- forma propia de ReadModels, espejo deliberado de
/// FranjaOrdinaria (Programacion.DomainEvents) sin importar ese tipo (MEF-ADR-0041 decision 1): el
/// consumidor debe poder responder "hay descansos o extras dentro de esta franja?" y "trae sede
/// prearmada?" sin ir al event store.
/// </summary>
/// <remarks>
/// Nombre "propuesta revisable" (issue #496, "Vista a materializar"): espejo de EtiquetaFicha en la
/// familia de tipos anidados de una ficha de catalogo.
/// </remarks>
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
/// Sub-franja (descanso o extra) contenida en una <see cref="FranjaFicha"/> -- forma propia de
/// ReadModels, espejo deliberado de SubFranjaProgramada (Programacion.DomainEvents) sin importar
/// ese tipo (MEF-ADR-0041 decision 1).
/// </summary>
public sealed record SubFranjaFicha(
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int DiaOffsetInicio,
    int DiaOffsetFin);
