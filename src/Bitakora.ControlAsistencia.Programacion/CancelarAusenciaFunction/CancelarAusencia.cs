namespace Bitakora.ControlAsistencia.Programacion.CancelarAusenciaFunction;

public record CancelarAusencia(string CodigoColaborador, Guid AusenciaId, DateOnly[] Fechas);
