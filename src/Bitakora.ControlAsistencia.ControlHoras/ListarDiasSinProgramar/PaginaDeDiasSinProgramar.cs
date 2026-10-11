namespace Bitakora.ControlAsistencia.ControlHoras.ListarDiasSinProgramar;

public static class PaginaDeDiasSinProgramar
{
    public const int TakeMaximo = 200;

    public static int? AcotarTake(int? take) => take is null ? null : Math.Clamp(take.Value, 1, TakeMaximo);

    public static (IReadOnlyList<T> Elementos, string? SiguienteCursor) Cortar<T>(
        IReadOnlyList<T> filasPedidas, int? take, Func<T, string> codigo)
    {
        if (take is null || filasPedidas.Count <= take.Value)
            return (filasPedidas, null);

        var pagina = filasPedidas.Take(take.Value).ToList();
        return (pagina, CursorOpaco.Codificar(codigo(pagina[^1])));
    }
}
