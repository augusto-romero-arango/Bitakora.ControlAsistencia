namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

/// <summary>
/// Representacion plana de una franja ordinaria propia de este ensamblado (issue #322, payload
/// por rol -- CA-ADR-0029 decision #5 / MEF-ADR-0039 decision #6). Duplica con paridad exacta de
/// campos a DetalleFranjaOrdinaria (PrivateEvents.Programacion) en vez de importarlo: los tres
/// ensamblados de eventos son tres islas sin referencias entre si.
/// </summary>
/// <remarks>
/// STUB de la fase roja (test-writer): declara la forma exacta de DetalleFranjaOrdinaria pero SIN
/// el override de Equals/GetHashCode que compara Descansos/Extras por valor (SequenceEqual). El
/// record por defecto compara esas IReadOnlyList por referencia (MEF-ADR-0012) -- replicar la
/// semantica correcta (CA-1 del issue #322) es responsabilidad del implementer, ver
/// FranjaProgramadaIgualdadTests.
/// </remarks>
public record FranjaProgramada(
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int DiaOffsetFin,
    IReadOnlyList<SubFranjaProgramada> Descansos,
    IReadOnlyList<SubFranjaProgramada> Extras,
    string Descripcion);
