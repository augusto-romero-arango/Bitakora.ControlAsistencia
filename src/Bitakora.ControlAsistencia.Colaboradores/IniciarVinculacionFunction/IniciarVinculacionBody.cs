namespace Bitakora.ControlAsistencia.Colaboradores.IniciarVinculacionFunction;

public record IniciarVinculacionBody(string CodigoColaborador, DateOnly FechaInicio, string? CodigoSede = null);
