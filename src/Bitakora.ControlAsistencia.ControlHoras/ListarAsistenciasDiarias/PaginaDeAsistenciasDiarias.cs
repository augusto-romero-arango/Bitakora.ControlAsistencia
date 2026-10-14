namespace Bitakora.ControlAsistencia.ControlHoras.ListarAsistenciasDiarias;

public static class PaginaDeAsistenciasDiarias
{
    public const int TakeMaximo = 200;

    public static bool TakeEnRango(int? take) => take is null or >= 1 and <= TakeMaximo;

    public static IReadOnlyList<FilaAsistenciaDiaria> PosterioresAlCursor(
        IReadOnlyList<FilaAsistenciaDiaria> filas, DateOnly? cursorFecha) =>
        filas.Where(f => cursorFecha is null || f.Fecha > cursorFecha).ToList();

    public static (IReadOnlyList<T> Elementos, string? SiguienteCursor) Cortar<T>(
        IReadOnlyList<T> filasPedidas, int? take, Func<T, string> cursorDe)
    {
        if (take is null || filasPedidas.Count <= take.Value)
            return (filasPedidas, null);

        var pagina = filasPedidas.Take(take.Value).ToList();
        return (pagina, cursorDe(pagina[^1]));
    }
}
