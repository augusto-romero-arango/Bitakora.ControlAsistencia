namespace Bitakora.ControlAsistencia.ControlHoras.ListarResumenesAsistencia;

public static class PaginaDeResumenesAsistencia
{
    public const int TakeMaximo = 200;

    public static bool TakeEnRango(int? take) => throw new NotImplementedException();

    public static IReadOnlyList<string> PosterioresAlCursor(
        IReadOnlyList<string> codigos, string? cursorCodigo) =>
        throw new NotImplementedException();

    public static (IReadOnlyList<T> Elementos, string? SiguienteCursor) Cortar<T>(
        IReadOnlyList<T> filasPedidas, int? take, Func<T, string> cursorDe) =>
        throw new NotImplementedException();
}
