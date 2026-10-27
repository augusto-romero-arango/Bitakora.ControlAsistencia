using Bitakora.ControlAsistencia.Programacion.ObtenerCuadroDeTurnos;
using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.ListarCuadrosDeTurnos;

public sealed record ListaCuadrosDeTurnosRespuesta(
    IReadOnlyList<CuadroDeTurnosRespuesta> Elementos, string? SiguienteCursor);

internal static class PaginaCuadrosDeTurnos
{
    public static ListaCuadrosDeTurnosRespuesta Componer(
        IEnumerable<CuadroDeTurnos> catalogo, int? take, CursorCuadroDeTurnos? cursor) =>
        throw new NotImplementedException();

    public static ListaCuadrosDeTurnosRespuesta Cortar(IReadOnlyList<CuadroDeTurnos> pedidas, int? take) =>
        throw new NotImplementedException();
}
