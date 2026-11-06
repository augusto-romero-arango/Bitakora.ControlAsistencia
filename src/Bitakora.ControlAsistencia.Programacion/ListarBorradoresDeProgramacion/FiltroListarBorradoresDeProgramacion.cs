namespace Bitakora.ControlAsistencia.Programacion.ListarBorradoresDeProgramacion;

public sealed record FiltroListarBorradoresDeProgramacion(
    DateOnly? FechaReferencia,
    IReadOnlyList<string>? Colaboradores = null,
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    int? Take = null,
    string? Cursor = null);
