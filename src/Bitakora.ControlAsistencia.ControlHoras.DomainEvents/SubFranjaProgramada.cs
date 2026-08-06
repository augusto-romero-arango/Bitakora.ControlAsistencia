namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

/// <summary>
/// Representacion plana de una sub-franja (descanso o extra) propia de este ensamblado
/// (issue #322, payload por rol -- CA-ADR-0029 decision #5 / MEF-ADR-0039 decision #6). Duplica
/// con paridad exacta de campos a DetalleSubFranja (PrivateEvents.Programacion) en vez de
/// importarlo: los tres ensamblados de eventos son tres islas sin referencias entre si.
/// </summary>
/// <remarks>
/// STUB de la fase roja (test-writer): declara la forma exacta de DetalleSubFranja pero SIN el
/// override de Equals/GetHashCode que excluye Descripcion de la identidad. Replicar esa semantica
/// (CA-1 del issue #322) es responsabilidad del implementer -- ver SubFranjaProgramadaIgualdadTests.
/// </remarks>
public record SubFranjaProgramada(
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int DiaOffsetInicio,
    int DiaOffsetFin,
    string Descripcion);
