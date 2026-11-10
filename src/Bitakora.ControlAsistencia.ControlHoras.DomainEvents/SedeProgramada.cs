namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

/// <summary>
/// Sede efectiva donde rige, para una franja del turno diario, el control persistido por este
/// dominio.
/// </summary>
/// <remarks>
/// Gemelo deliberado de SedeProgramada (Programacion.DomainEvents) y de DetalleSede
/// (PrivateEvents.Programacion): payload por rol, tres islas sin referencias entre si
/// (CA-ADR-0029 decision #5). ControlHoras solo persiste la sede efectiva ya resuelta por
/// Programacion; por eso no expone EstaCompleta(): esa regla es de quien compone la sede.
/// </remarks>
public record SedeProgramada(string Id, string Nombre, string? CentroDeCostos = null);
