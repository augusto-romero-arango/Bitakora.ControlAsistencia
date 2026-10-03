namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public record AusenciaProgramada(
    Guid AusenciaId,
    ColaboradorProgramado Colaborador,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    MotivoAusencia Motivo);
