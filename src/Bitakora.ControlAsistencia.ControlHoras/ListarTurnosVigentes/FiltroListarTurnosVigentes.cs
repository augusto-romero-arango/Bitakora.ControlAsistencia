namespace Bitakora.ControlAsistencia.ControlHoras.ListarTurnosVigentes;

public sealed record FiltroListarTurnosVigentes(
    DateOnly? DesdeFecha,
    DateOnly? HastaFecha,
    IReadOnlyList<string>? CodigosColaborador,
    string? SedeId,
    int? Take = null,
    string? Cursor = null);
