namespace Bitakora.ControlAsistencia.ControlHoras.ListarAsistenciasDiarias;

public static class PaginaDeAsistenciasDiarias
{
    public const int TakeMaximo = 200;

    public static bool TakeEnRango(int? take) => throw new NotImplementedException();

    public static IReadOnlyList<FilaAsistenciaDiaria> PosterioresAlCursor(
        IReadOnlyList<FilaAsistenciaDiaria> filas, DateOnly? cursorFecha) =>
        throw new NotImplementedException();

    public static (IReadOnlyList<T> Elementos, string? SiguienteCursor) Cortar<T>(
        IReadOnlyList<T> filasPedidas, int? take, Func<T, string> cursorDe) =>
        throw new NotImplementedException();
}
