namespace Bitakora.ControlAsistencia.Programacion.ProgramarAusenciaFunction;

public record ProgramarAusencia(
    Guid Id,
    string CodigoColaborador,
    string Identificacion,
    string NombreCompleto,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    string Motivo);
