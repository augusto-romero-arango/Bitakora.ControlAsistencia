namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

/// <summary>
/// Nacimiento de un colaborador bajo control de asistencia; se persiste en el mismo commit que
/// VinculacionIniciada.
/// </summary>
public sealed record ColaboradorRegistrado(Identificacion Identificacion, NombreColaborador Nombre);
