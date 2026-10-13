using Bitakora.ControlAsistencia.Programacion.ObtenerCuadroSemanalTurnos;
using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.ListarCuadrosSemanalesTurnos;

public sealed record ListaCuadrosSemanalesTurnosRespuesta(
    IReadOnlyList<CuadroSemanalTurnosRespuesta> Elementos, string? SiguienteCursor);

internal static class PaginaCuadrosSemanalesTurnos
{
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
