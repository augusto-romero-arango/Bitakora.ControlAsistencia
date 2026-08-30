namespace Bitakora.ControlAsistencia.Programacion.CancelarProgramacionFunction;

public record CancelarProgramacion(
    Guid Id,
    ColaboradorSolicitado Colaborador,
    List<DateOnly> Fechas);
