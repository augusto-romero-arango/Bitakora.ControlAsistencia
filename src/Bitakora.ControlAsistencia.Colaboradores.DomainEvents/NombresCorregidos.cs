namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

/// <summary>
/// Correccion de los nombres de un colaborador: un nombre mal digitado, o el del dueno legitimo del
/// documento tras reusar el stream.
/// </summary>
public sealed record NombresCorregidos(NombreColaborador Nombre);
