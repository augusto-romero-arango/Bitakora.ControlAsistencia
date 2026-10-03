namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Stub de #744 (CA-ADR-0036 decision 7: se cancela por fechas): la ausencia pierde las Fechas
// indicadas. Forma asumida por la fase roja de #755; el implementer de #744/#755 la confirma y la
// registra en IdentidadEventosProgramacion.TiposPersistidos.
public record AusenciaCancelada(
    Guid AusenciaId,
    ColaboradorProgramado Colaborador,
    IReadOnlyList<DateOnly> Fechas);
