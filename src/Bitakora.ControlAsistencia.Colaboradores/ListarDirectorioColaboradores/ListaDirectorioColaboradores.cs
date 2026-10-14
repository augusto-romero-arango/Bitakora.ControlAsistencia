namespace Bitakora.ControlAsistencia.Colaboradores.ListarDirectorioColaboradores;

public sealed record ListaDirectorioColaboradores(
    IReadOnlyList<DirectorioColaboradorRespuesta> Elementos, string? SiguienteCursor);
