namespace Bitakora.ControlAsistencia.Programacion.ProgramarAusenciaFunction;

public record ProgramarAusenciaBody(
    Guid Id,
    string Identificacion,
    string NombreCompleto,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    string Motivo);
