namespace Bitakora.ControlAsistencia.ControlHoras.ListarMallaSemanalTurnos;

public sealed record ColaboradorVigente(string CodigoColaborador, DateOnly VigenteDesde, DateOnly? VigenteHasta)
{
    public bool EstaVinculadoEl(DateOnly fecha) =>
        fecha >= VigenteDesde && (VigenteHasta is null || fecha <= VigenteHasta);
}

public sealed record FiltroListarMallaSemanalTurnos(
    DateOnly? Fecha,
    IReadOnlyList<ColaboradorVigente>? Colaboradores,
    int? Take = null,
    string? Cursor = null);
