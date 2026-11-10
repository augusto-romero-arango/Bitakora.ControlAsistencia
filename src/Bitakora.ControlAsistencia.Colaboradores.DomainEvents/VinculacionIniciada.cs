namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

/// <summary>
/// Inicio de una vinculacion del colaborador. Lo emiten tanto el registro (en el mismo commit que
/// ColaboradorRegistrado) como el reingreso: un evento no conoce su comando (CA-ADR-0029).
/// </summary>
/// <remarks>
/// El default null de CodigoSede es lo que hace compatible la evolucion aditiva (MEF-ADR-0005): sin
/// el, los eventos ya persistidos sin ese campo no deserializan.
/// </remarks>
public sealed record VinculacionIniciada(string Codigo, DateOnly FechaInicio, string? CodigoSede = null);
