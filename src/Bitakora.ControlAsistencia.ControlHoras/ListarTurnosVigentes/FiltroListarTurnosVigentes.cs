namespace Bitakora.ControlAsistencia.ControlHoras.ListarTurnosVigentes;

public sealed record FiltroListarTurnosVigentes(
    DateOnly? DesdeFecha,
    DateOnly? HastaFecha,
    string? CodigoColaborador,
    string? SedeId,
    int? Take = null,
    string? Cursor = null);
