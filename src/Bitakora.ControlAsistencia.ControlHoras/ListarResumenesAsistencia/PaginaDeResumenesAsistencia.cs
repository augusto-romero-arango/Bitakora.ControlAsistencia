namespace Bitakora.ControlAsistencia.ControlHoras.ListarResumenesAsistencia;

public static class PaginaDeResumenesAsistencia
{
    public const int TakeMaximo = 200;

    public static bool TakeEnRango(int? take) => take is null or >= 1 and <= TakeMaximo;

    public static IReadOnlyList<string> PosterioresAlCursor(
        IReadOnlyList<string> codigos, string? cursorCodigo) =>
        codigos
            .Distinct(StringComparer.Ordinal)
            .OrderBy(codigo => codigo, StringComparer.Ordinal)
            .Where(codigo => cursorCodigo is null || string.CompareOrdinal(codigo, cursorCodigo) > 0)
            .ToList();

    public static (IReadOnlyList<T> Elementos, string? SiguienteCursor) Cortar<T>(
        IReadOnlyList<T> filasPedidas, int? take, Func<T, string> cursorDe)
    {
        if (take is null || filasPedidas.Count <= take.Value)
            return (filasPedidas, null);

        var pagina = filasPedidas.Take(take.Value).ToList();
        return (pagina, cursorDe(pagina[^1]));
    }
}
