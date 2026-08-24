namespace Bitakora.ControlAsistencia.ControlHoras.ListarResumenesAsistencia;

/// <summary>
/// Paso 1 de la paginacion keyset: la pagina de codigos de colaborador sobre la que despues se
/// agregan los documentos. Logica pura -- la rama que descubre el universo en Marten vive en el
/// endpoint, pero la cota del Take es de las dos.
/// </summary>
public static class PaginaDeCodigos
{
    /// <summary>Tope de pagina del servidor (MEF-ADR-0042 seccion 2).</summary>
    public const int TakeMaximo = 200;

    /// <summary>
    /// El Take llega en el body y es entrada del cliente como cualquier otra: sin esta cota, un
    /// cliente pide una pagina sin limite y el Take deja de acotar nada.
    /// </summary>
    public static int AcotarTake(int take) => Math.Clamp(take, 1, TakeMaximo);

    /// <summary>
    /// Recorta la lista de codigos que el cliente ya trajo -- sin consultar Marten: quien manda
    /// CodigosColaborador ya conoce el universo. Ordena ascendente ordinal, descarta lo que el
    /// cursor deja atras y corta en <paramref name="take"/>.
    /// </summary>
    public static IReadOnlyList<string> Recortar(
        IReadOnlyList<string> codigos, string? cursor, int take)
    {
        IEnumerable<string> ordenados = codigos
            .Distinct(StringComparer.Ordinal)
            .OrderBy(codigo => codigo, StringComparer.Ordinal);

        if (cursor is not null)
            ordenados = ordenados.Where(codigo => string.CompareOrdinal(codigo, cursor) > 0);

        return ordenados.Take(AcotarTake(take)).ToList();
    }
}
