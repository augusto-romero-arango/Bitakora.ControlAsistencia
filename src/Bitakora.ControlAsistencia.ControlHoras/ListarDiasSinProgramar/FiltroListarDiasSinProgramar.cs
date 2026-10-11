namespace Bitakora.ControlAsistencia.ControlHoras.ListarDiasSinProgramar;

public sealed record ColaboradorVigente(string CodigoColaborador, DateOnly VigenteDesde, DateOnly? VigenteHasta);

public sealed record FiltroListarDiasSinProgramar(
    DateOnly? DesdeFecha,
    DateOnly? HastaFecha,
    IReadOnlyList<ColaboradorVigente>? Colaboradores,
    int? Take = null,
    string? Cursor = null);
