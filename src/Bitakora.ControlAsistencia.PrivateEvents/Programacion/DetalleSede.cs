namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

/// <summary>
/// Gemelo deliberado de SedeProgramada (Programacion.DomainEvents) con paridad exacta de campos, sin
/// referenciarlo (CA-ADR-0029: tres islas). Solo strings: la igualdad por valor del record ya es correcta.
/// </summary>
public record DetalleSede(string Id, string Nombre, string? CentroDeCostos = null);
