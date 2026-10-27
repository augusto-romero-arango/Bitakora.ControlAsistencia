using Bitakora.ControlAsistencia.Programacion.ObtenerCuadroDeTurnos;
using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.ListarCuadrosDeTurnos;

public sealed record ListaCuadrosDeTurnosRespuesta(
    IReadOnlyList<CuadroDeTurnosRespuesta> Elementos, string? SiguienteCursor);

internal static class PaginaCuadrosDeTurnos
{
    // El orden (Nombre, Id) es ordinal y se aplica en memoria: el catalogo es acotado y asi el corte
    // keyset no depende de la colacion de la base.
    public static ListaCuadrosDeTurnosRespuesta Componer(
        IEnumerable<CuadroDeTurnos> catalogo, int? take, CursorCuadroDeTurnos? cursor)
    {
        var posteriores = catalogo
            .OrderBy(x => x.Nombre, StringComparer.Ordinal).ThenBy(x => x.Id, StringComparer.Ordinal)
            .Where(x => cursor is not { } c
                || string.CompareOrdinal(x.Nombre, c.Nombre) > 0
                || (x.Nombre == c.Nombre && string.CompareOrdinal(x.Id, c.Id) > 0));

        return Cortar((take is { } t ? posteriores.Take(t + 1) : posteriores).ToList(), take);
    }

    public static ListaCuadrosDeTurnosRespuesta Cortar(IReadOnlyList<CuadroDeTurnos> pedidas, int? take)
    {
        var pagina = take is { } tope ? pedidas.Take(tope).ToList() : pedidas.ToList();
        var siguienteCursor = take is { } t && pedidas.Count > t
            ? new CursorCuadroDeTurnos(pagina[^1].Nombre, pagina[^1].Id).Codificar()
            : null;

        return new ListaCuadrosDeTurnosRespuesta(
            pagina.Select(CuadroDeTurnosRespuesta.Componer).ToList(), siguienteCursor);
    }
}
