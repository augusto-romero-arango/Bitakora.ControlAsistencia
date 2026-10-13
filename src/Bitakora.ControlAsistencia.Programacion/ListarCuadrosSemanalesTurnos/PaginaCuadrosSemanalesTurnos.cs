using Bitakora.ControlAsistencia.Programacion.ObtenerCuadroSemanalTurnos;
using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.ListarCuadrosSemanalesTurnos;

public sealed record ListaCuadrosSemanalesTurnosRespuesta(
    IReadOnlyList<CuadroSemanalTurnosRespuesta> Elementos, string? SiguienteCursor);

internal static class PaginaCuadrosSemanalesTurnos
{
    public static ListaCuadrosSemanalesTurnosRespuesta Cortar(IReadOnlyList<CuadroSemanalTurnos> pedidas, int? take) =>
        throw new NotImplementedException();
}
