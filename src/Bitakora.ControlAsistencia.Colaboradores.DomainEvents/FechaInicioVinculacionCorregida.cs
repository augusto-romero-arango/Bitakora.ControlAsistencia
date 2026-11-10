namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

/// <summary>
/// Correccion de la fecha de inicio de la ULTIMA vinculacion de un colaborador, tenga o no
/// terminacion registrada.
/// </summary>
/// <remarks>
/// No reutiliza VinculacionIniciada: es la enmienda de un dato existente, no el nacimiento de una
/// vinculacion nueva (CA-ADR-0029).
/// </remarks>
public sealed record FechaInicioVinculacionCorregida(DateOnly FechaInicio);
