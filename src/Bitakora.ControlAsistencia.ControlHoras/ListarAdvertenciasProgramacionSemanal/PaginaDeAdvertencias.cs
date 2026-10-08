namespace Bitakora.ControlAsistencia.ControlHoras.ListarAdvertenciasProgramacionSemanal;

public static class PaginaDeAdvertencias
{
    public const int TakeMaximo = 200;

    /// <summary>null = sin Take (resultado completo); con valor, acota a 1..200.</summary>
    public static int? AcotarTake(int? take) => take is null ? null : Math.Clamp(take.Value, 1, TakeMaximo);

    /// <summary>Recibe las filas pedidas con Take+1 y devuelve la pagina y el cursor opaco si hay mas.</summary>
    public static (IReadOnlyList<T> Elementos, string? SiguienteCursor) Cortar<T>(
        IReadOnlyList<T> filasPedidas, int? take, Func<T, string> codigo)
    {
        if (take is null || filasPedidas.Count <= take.Value)
            return (filasPedidas, null);

        var pagina = filasPedidas.Take(take.Value).ToList();
        return (pagina, CursorOpaco.Codificar(codigo(pagina[^1])));
    }
}
