namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

/// <summary>
/// Representacion plana de la sede que viaja en eventos privados intra-BC.
/// </summary>
/// <remarks>
/// Issue #331: payload por rol (CA-ADR-0029 decision #5) -- gemelo deliberado de SedeProgramada
/// (Programacion.DomainEvents) con paridad exacta de campos, sin referenciarlo: los ensamblados de
/// eventos son tres islas sin referencias entre si. Todos los campos son string: portable por el
/// serializador por defecto del bus (MEF-ADR-0023/0024). Sin Equals custom: la igualdad por valor
/// del record por defecto ya es correcta (mismo criterio que ResumenColaborador, issue #421).
/// Issue #462: CentroDeCostos es opcional y aditivo, gemelo del campo que ganan los otros dos
/// integrantes del trio (SedeProgramada de Programacion.DomainEvents y de ControlHoras.DomainEvents).
/// </remarks>
public record DetalleSede(string Id, string Nombre, string? CentroDeCostos = null);
