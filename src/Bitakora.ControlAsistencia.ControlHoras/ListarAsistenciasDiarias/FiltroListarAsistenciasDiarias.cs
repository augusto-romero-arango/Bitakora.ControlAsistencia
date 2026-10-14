namespace Bitakora.ControlAsistencia.ControlHoras.ListarAsistenciasDiarias;

public sealed record FiltroListarAsistenciasDiarias(
    string? CodigoColaborador,
    DateOnly? DesdeFecha,
    DateOnly? HastaFecha,
    int? Take = null,
    string? Cursor = null);
