namespace Bitakora.ControlAsistencia.ControlHoras.ListarMallaSemanalTurnos;

public static class PaginaDeMallaSemanal
{
    public const int TakeMaximo = 200;

    public static int? AcotarTake(int? take) => throw new NotImplementedException();

    public static IReadOnlyList<ColaboradorVigente> PosterioresAlCursor(
        IEnumerable<ColaboradorVigente> colaboradores, string? cursorCodigo) =>
        throw new NotImplementedException();

    public static (IReadOnlyList<T> Elementos, string? SiguienteCursor) Cortar<T>(
        IReadOnlyList<T> filasPedidas, int? take, Func<T, string> codigo) =>
        throw new NotImplementedException();
}
