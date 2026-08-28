namespace Bitakora.ControlAsistencia.ReadModels.Sedes;

/// <summary>
/// Read model del estado vigente de una sede (issue #461), consumido por el maestro de sedes
/// (correccion/activacion/dispositivos) y por las pantallas de asignacion, que solo deben ver
/// sedes ACTIVAS (Programacion de turnos hoy, la futura asignacion de colaborador a sede en #465).
/// </summary>
/// <remarks>
/// Record plano SIN partial (MEF-ADR-0035, skills/projections/modelos-marten.md): el comportamiento
/// de proyeccion vive en la clase companion FichaSedeProjection, en el worker
/// (Bitakora.ControlAsistencia.Projections). Este tipo vive en ReadModels, la cuarta isla del repo
/// -- cero referencias de proyecto (ver el .csproj).
///
/// Sin sufijo "View" (MEF-ADR-0041 decision 3, extension al read-side, precedentes TurnoVigente/
/// FichaColaborador).
///
/// No es calco 1:1 del aggregate (issue #461, "Vista a materializar"): consolida el estado vigente
/// desde 9 tipos de evento y agrega la dimension de asignabilidad (Activa) que las pantallas
/// necesitan -- Activa es filtro del read model, no invariante del write-side (sin rechazo en
/// servidor, #459).
///
/// Id es el stream key que compone SedeAggregateRoot.ComputarStreamId(Codigo): "s:{codigo}" (CA-
/// ADR-0031) -- distinto de <see cref="Codigo"/>, que es el termino del vocabulario del consumidor
/// (el codigo puro, sin el prefijo de anatomia de clave). ObtenerFichaSede recomputa el stream key a
/// partir del {codigo} de ruta con el mismo metodo del aggregate, nunca partiendo el Id a mano
/// (MEF-ADR-0037).
/// </remarks>
public sealed record FichaSede(
    string Id,
    string Codigo,
    string Nombre,
    string? Ciudad,
    string? Direccion,
    string? CentroDeCostos,
    bool Activa,
    IReadOnlyList<string> Dispositivos);
