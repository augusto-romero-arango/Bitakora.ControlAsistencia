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
    {
        var pascua = CalcularPascua(año);

        // 8 festivos fijos: 6 de fecha fija + Jueves y Viernes Santo (derivados de Pascua, no trasladables).
        var fijos = new[]
        {
            new DateOnly(año, 1, 1),    // Año Nuevo
            new DateOnly(año, 5, 1),    // Dia del Trabajo
            new DateOnly(año, 7, 20),   // Independencia de Colombia
            new DateOnly(año, 8, 7),    // Batalla de Boyaca
            new DateOnly(año, 12, 8),   // Inmaculada Concepcion
            new DateOnly(año, 12, 25),  // Navidad
            pascua.AddDays(-3),         // Jueves Santo
            pascua.AddDays(-2),         // Viernes Santo
        };

        // 10 festivos trasladables: 7 de fecha fija + 3 derivados de Pascua.
        var trasladablesBase = new[]
        {
            new DateOnly(año, 1, 6),    // Reyes Magos
            new DateOnly(año, 3, 19),   // San Jose
            new DateOnly(año, 6, 29),   // San Pedro y San Pablo
            new DateOnly(año, 8, 15),   // Asuncion de la Virgen
            new DateOnly(año, 10, 12),  // Dia de la Raza
            new DateOnly(año, 11, 1),   // Todos los Santos
            new DateOnly(año, 11, 11),  // Independencia de Cartagena
            pascua.AddDays(39),         // Ascension del Senor
            pascua.AddDays(60),         // Corpus Christi
            pascua.AddDays(68),         // Sagrado Corazon de Jesus
        };

        return fijos
            .Concat(trasladablesBase.Select(TrasladarAlLunes))
            .OrderBy(f => f)
            .ToArray();
    }

    /// <summary>
    /// Retorna true si la fecha es festivo nacional colombiano.
    /// </summary>
    public static bool EsFestivo(DateOnly fecha)
        => ObtenerFestivos(fecha.Year).Contains(fecha);

    // Algoritmo de Computus anonimo gregoriano (Meeus/Jones/Butcher).
    // Calcula la fecha de Pascua para el año gregoriano indicado.
    // Referencia: Jean Meeus, Astronomical Algorithms, 2da ed., cap. 9.
    private static DateOnly CalcularPascua(int año)
    {
        var a = año % 19;
        var b = año / 100;
        var c = año % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var mes = (h + l - 7 * m + 114) / 31;
        var dia = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateOnly(año, mes, dia);
    }

    // Ley Emiliani (Ley 51 de 1983 art. 1):
    // - Si la fecha cae lunes, permanece ese lunes.
    // - Si cae cualquier otro dia, se traslada al lunes inmediatamente siguiente.
    private static DateOnly TrasladarAlLunes(DateOnly fecha)
    {
        var diasHastaLunes = ((int)DayOfWeek.Monday - (int)fecha.DayOfWeek + 7) % 7;
        return fecha.AddDays(diasHastaLunes);
    }
}
