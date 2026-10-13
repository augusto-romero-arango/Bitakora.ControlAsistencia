namespace Bitakora.ControlAsistencia.ControlHoras.ListarTurnosVigentes;

public static class PaginaDeTurnosVigentes
{
    public const int TakeMaximo = 200;

    public static bool TakeEnRango(int? take) => take is null or >= 1 and <= TakeMaximo;

    public static (IReadOnlyList<T> Elementos, string? SiguienteCursor) Cortar<T>(
        IReadOnlyList<T> filasPedidas, int? take, Func<T, string> cursorDe)
    {
        if (take is null || filasPedidas.Count <= take.Value)
            return (filasPedidas, null);

        var pagina = filasPedidas.Take(take.Value).ToList();
        return (pagina, cursorDe(pagina[^1]));
    }
}
