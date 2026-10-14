using Bitakora.ControlAsistencia.ReadModels.Sedes;

namespace Bitakora.ControlAsistencia.Sedes.ListarFichasSede;

public sealed record ListaFichasSedeRespuesta(IReadOnlyList<FichaSede> Elementos, string? SiguienteCursor);

internal static class PaginaFichasSede
{
    public static ListaFichasSedeRespuesta Componer(
        IEnumerable<FichaSede> catalogo, int? take, CursorFichaSede? cursor)
    {
        var ordenada = catalogo
            .OrderBy(f => f.Codigo, StringComparer.Ordinal)
            .ThenBy(f => f.Id, StringComparer.Ordinal);

        var filtradas = cursor is { } c
            ? ordenada.Where(f =>
                string.CompareOrdinal(f.Codigo, c.Codigo) > 0
                || (f.Codigo == c.Codigo && string.CompareOrdinal(f.Id, c.Id) > 0))
            : ordenada;

        var pedidas = take is { } t ? filtradas.Take(t + 1).ToList() : filtradas.ToList();
        return Cortar(pedidas, take);
    }

    public static ListaFichasSedeRespuesta Cortar(IReadOnlyList<FichaSede> pedidas, int? take)
    {
        if (take is not { } tope || pedidas.Count <= tope)
            return new ListaFichasSedeRespuesta(pedidas, null);

        var elementos = pedidas.Take(tope).ToList();
        var ultima = elementos[^1];
        return new ListaFichasSedeRespuesta(elementos, new CursorFichaSede(ultima.Codigo, ultima.Id).Codificar());
    }
}
