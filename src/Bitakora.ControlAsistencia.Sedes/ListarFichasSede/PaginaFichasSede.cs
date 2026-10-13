using Bitakora.ControlAsistencia.ReadModels.Sedes;

namespace Bitakora.ControlAsistencia.Sedes.ListarFichasSede;

public sealed record ListaFichasSedeRespuesta(IReadOnlyList<FichaSede> Elementos, string? SiguienteCursor);

internal static class PaginaFichasSede
{
    public static ListaFichasSedeRespuesta Componer(
        IEnumerable<FichaSede> catalogo, int? take, CursorFichaSede? cursor) =>
        throw new NotImplementedException();

    public static ListaFichasSedeRespuesta Cortar(IReadOnlyList<FichaSede> pedidas, int? take) =>
        throw new NotImplementedException();
}
