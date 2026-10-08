namespace Bitakora.ControlAsistencia.ControlHoras.ListarAdvertenciasProgramacionSemanal;

public sealed record FiltroListarAdvertenciasProgramacionSemanal(
    DateOnly? Fecha,
    IReadOnlyList<string>? CodigosColaborador,
    bool SoloConAdvertencias = true,
    int? Take = null,
    string? Cursor = null);
