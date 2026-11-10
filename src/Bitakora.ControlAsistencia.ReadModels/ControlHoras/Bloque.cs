namespace Bitakora.ControlAsistencia.ReadModels.ControlHoras;

/// <summary>
/// Bloque de tiempo absoluto (hora local del tenant) del turno vigente; espejo de <c>BloqueTurno</c>
/// (ControlHoras.DomainEvents), que la proyeccion mapea a este tipo.
/// </summary>
/// <remarks>
/// La sede va POR BLOQUE, nunca por dia: un dia multi-sede no tiene "una" sede. SedeId/NombreSede
/// son null si la franja no tiene sede asignada o el evento es anterior a que existiera el campo.
/// </remarks>
public sealed record Bloque(
    TipoBloque Tipo,
    DateTime Inicio,
    DateTime Fin,
    string? SedeId = null,
    string? NombreSede = null);
