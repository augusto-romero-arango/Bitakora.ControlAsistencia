using Bitakora.ControlAsistencia.Programacion.ObtenerCuadroSemanalTurnos;
using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.ListarCuadrosSemanalesTurnos;

public sealed record ListaCuadrosSemanalesTurnosRespuesta(
    IReadOnlyList<CuadroSemanalTurnosRespuesta> Elementos, string? SiguienteCursor);

internal static class PaginaCuadrosSemanalesTurnos
{
    // El orden (Nombre, Id) es ordinal y se aplica en memoria: el catalogo es acotado y asi el corte
    // keyset no depende de la colacion de la base.
    public static ListaCuadrosSemanalesTurnosRespuesta Componer(
        IEnumerable<CuadroSemanalTurnos> catalogo, int? take, CursorCuadroSemanalTurnos? cursor)
    {
        var posteriores = catalogo
            .OrderBy(x => x.Nombre, StringComparer.Ordinal).ThenBy(x => x.Id, StringComparer.Ordinal)
            .Where(x => cursor is not { } c
                || string.CompareOrdinal(x.Nombre, c.Nombre) > 0
                || (x.Nombre == c.Nombre && string.CompareOrdinal(x.Id, c.Id) > 0));

        return Cortar((take is { } t ? posteriores.Take(t + 1) : posteriores).ToList(), take);
    }

    public static ListaCuadrosSemanalesTurnosRespuesta Cortar(IReadOnlyList<CuadroSemanalTurnos> pedidas, int? take)
    {
        var pagina = take is { } tope ? pedidas.Take(tope).ToList() : pedidas.ToList();
        var siguienteCursor = take is { } t && pedidas.Count > t
            ? new CursorCuadroSemanalTurnos(pagina[^1].Nombre, pagina[^1].Id).Codificar()
            : null;

        return new ListaCuadrosSemanalesTurnosRespuesta(
            pagina.Select(CuadroSemanalTurnosRespuesta.Componer).ToList(), siguienteCursor);
    }
}
