namespace Bitakora.ControlAsistencia.ReadModels.Sedes;

/// <summary>
/// Estado vigente de una sede, para el maestro de sedes y para las pantallas de asignacion, que solo
/// deben ver sedes activas.
/// </summary>
/// <remarks>
/// Activa es filtro del read model, no invariante del write-side: el servidor no rechaza por ella.
///
/// Id es el stream key "s:{codigo}" (CA-ADR-0031), distinto de <see cref="Codigo"/>, el codigo puro;
/// el Id se recomputa con SedeAggregateRoot.ComputarStreamId, nunca se parte a mano.
/// </remarks>
[MaterializadoPorProyeccion]
public sealed record FichaSede(
    string Id,
    string Codigo,
    string Nombre,
    string? Ciudad,
    string? Direccion,
    string? CentroDeCostos,
    bool Activa,
    IReadOnlyList<string> Dispositivos);
