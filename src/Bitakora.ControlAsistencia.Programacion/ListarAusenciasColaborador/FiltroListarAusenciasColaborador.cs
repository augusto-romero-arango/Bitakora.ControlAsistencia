namespace Bitakora.ControlAsistencia.Programacion.ListarAusenciasColaborador;

public sealed record FiltroListarAusenciasColaborador(DateOnly? Desde, DateOnly? Hasta, int? Take = null, string? Cursor = null);
