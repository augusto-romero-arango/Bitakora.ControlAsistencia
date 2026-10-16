using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.ListarFichasTurno;

public sealed record ListaFichasTurnoRespuesta(IReadOnlyList<FichaTurno> Elementos, string? SiguienteCursor);

internal static class PaginaFichasTurno
{
    // El orden (Nombre, Id) es ordinal y se aplica en memoria: el catalogo es acotado y asi el corte
    // keyset no depende de la colacion de la base.
    public static ListaFichasTurnoRespuesta Componer(
        IEnumerable<FichaTurno> catalogo, int? take, CursorFichaTurno? cursor, string? sedeId = null)
    {
        var posteriores = catalogo
            .OrderBy(f => f.Nombre, StringComparer.Ordinal).ThenBy(f => f.Id, StringComparer.Ordinal)
            .Where(f => cursor is not { } c
                || string.CompareOrdinal(f.Nombre, c.Nombre) > 0
                || (f.Nombre == c.Nombre && string.CompareOrdinal(f.Id, c.Id) > 0));

        return Cortar((take is { } t ? posteriores.Take(t + 1) : posteriores).ToList(), take);
    }

    public static ListaFichasTurnoRespuesta Cortar(IReadOnlyList<FichaTurno> pedidas, int? take)
    {
        if (take is not { } tope || pedidas.Count <= tope)
            return new ListaFichasTurnoRespuesta(pedidas, null);

        var pagina = pedidas.Take(tope).ToList();
        var ultima = pagina[^1];
        return new ListaFichasTurnoRespuesta(pagina, new CursorFichaTurno(ultima.Nombre, ultima.Id).Codificar());
    }
}
