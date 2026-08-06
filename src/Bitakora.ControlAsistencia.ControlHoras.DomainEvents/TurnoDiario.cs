namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

/// <summary>
/// Representacion plana del turno que rige efectivamente a un empleado en una fecha concreta,
/// propia de este ensamblado (issue #322, payload por rol -- CA-ADR-0029 decision #5 /
/// MEF-ADR-0039 decision #6). Duplica con paridad exacta de campos a DetalleTurno
/// (PrivateEvents.Programacion) en vez de importarlo: los tres ensamblados de eventos son tres
/// islas sin referencias entre si. Nombre acordado con el usuario (docs/eda/ubiquitous-language.yaml):
/// "TurnoDiario" es el termino exacto del glosario para este concepto.
/// </summary>
/// <remarks>
/// STUB de la fase roja (test-writer): declara la forma exacta de DetalleTurno pero SIN el
/// override de Equals/GetHashCode que compara FranjasOrdinarias por valor (SequenceEqual) y
/// excluye Descripcion de la identidad del turno. Replicar esa semantica (CA-1 del issue #322) es
/// responsabilidad del implementer -- ver TurnoDiarioIgualdadTests.
/// </remarks>
public record TurnoDiario(
    string Nombre,
    IReadOnlyList<FranjaProgramada> FranjasOrdinarias,
    string Descripcion);
