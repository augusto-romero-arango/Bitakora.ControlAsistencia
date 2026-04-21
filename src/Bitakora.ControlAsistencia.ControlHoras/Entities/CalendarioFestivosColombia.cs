// HU-113: Calcular festivos colombianos por año
// Referencia legal: Ley 51 de 1983 art. 1 (Ley Emiliani), art. 177 CST
// Algoritmo de Computus: Meeus/Jones/Butcher (Jean Meeus, Astronomical Algorithms, cap. 9)
namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

/// <summary>
/// Calcula los 18 festivos nacionales de Colombia para un año dado.
/// 8 festivos fijos + 10 trasladables (Ley Emiliani: se mueven al lunes siguiente).
/// </summary>
public static class CalendarioFestivosColombia
{
    /// <summary>
    /// Retorna los 18 festivos del año ordenados cronologicamente.
    /// </summary>
    public static IReadOnlyList<DateOnly> ObtenerFestivos(int año)
        => throw new NotImplementedException();

    /// <summary>
    /// Retorna true si la fecha es festivo nacional colombiano.
    /// </summary>
    public static bool EsFestivo(DateOnly fecha)
        => throw new NotImplementedException();

    // Algoritmo de Computus anonimo gregoriano (Meeus/Jones/Butcher).
    // Calcula la fecha de Pascua para el año gregoriano indicado.
    // Referencia: Jean Meeus, Astronomical Algorithms, 2da ed., cap. 9.
    private static DateOnly CalcularPascua(int año)
        => throw new NotImplementedException();

    // Ley Emiliani (Ley 51 de 1983 art. 1):
    // - Si la fecha cae lunes, permanece ese lunes.
    // - Si cae cualquier otro dia, se traslada al lunes inmediatamente siguiente.
    private static DateOnly TrasladarAlLunes(DateOnly fecha)
        => throw new NotImplementedException();
}
