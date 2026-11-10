namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// La ausencia pierde solo las Fechas indicadas: se cancela por fechas (CA-ADR-0036 decision 7).
public record AusenciaCancelada(
    Guid AusenciaId,
    ColaboradorProgramado Colaborador,
    IReadOnlyList<DateOnly> Fechas);
