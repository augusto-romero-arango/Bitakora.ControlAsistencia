namespace Bitakora.ControlAsistencia.ControlHoras.ListarResumenesAsistencia;

public sealed record FiltroListarResumenesAsistencia(
    DateOnly? DesdeFecha,
    DateOnly? HastaFecha,
    IReadOnlyList<string>? CodigosColaborador,
    string? Cursor,
    int? Take = null);
