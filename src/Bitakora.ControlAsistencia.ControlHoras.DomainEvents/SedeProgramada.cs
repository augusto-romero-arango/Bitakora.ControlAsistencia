namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

/// <summary>
/// Sede efectiva donde rige, para una franja del turno diario, el control persistido por este
/// dominio.
/// </summary>
/// <remarks>
/// Issue #336: gemelo deliberado de SedeProgramada (Programacion.DomainEvents, issue #331) y de
/// DetalleSede (PrivateEvents.Programacion, issue #331) -- payload por rol, tres islas sin
/// referencias entre si (CA-ADR-0029 decision #5 / MEF-ADR-0039 decision #6). ControlHoras solo
/// persiste la sede EFECTIVA ya resuelta por la cascada del lado de Programacion (#341); nunca la
/// valida ni la deriva -- por eso, a diferencia del gemelo de Programacion, no expone
/// EstaCompleta() (esa regla de completitud pertenece a quien COMPONE la sede, no a quien
/// solamente la transporta y persiste).
/// Sin Equals custom: todos los campos son string, la igualdad por valor del record por defecto ya
/// es correcta (mismo criterio que ColaboradorProgramado, issue #319, y SedeProgramada de
/// Programacion, #331).
/// </remarks>
public record SedeProgramada(string Id, string Nombre, string? CentroDeCostos = null);
