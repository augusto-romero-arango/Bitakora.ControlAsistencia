using Bitakora.ControlAsistencia.ReadModels.Colaboradores;

namespace Bitakora.ControlAsistencia.Colaboradores.ListarCategoriasDeEtiquetas;

public sealed record ListaCategoriasDeEtiquetas(IReadOnlyList<CategoriaDeEtiquetas> Elementos, string? SiguienteCursor);

public static class CursorCategoria
{
    public static string Codificar(string id) => throw new NotImplementedException();

    public static bool TryDecodificar(string cursor, out string id) => throw new NotImplementedException();
}
