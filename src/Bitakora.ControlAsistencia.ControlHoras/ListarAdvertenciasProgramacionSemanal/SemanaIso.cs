using System.Globalization;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarAdvertenciasProgramacionSemanal;

public sealed record SemanaIso(int Anio, int Numero, DateOnly Lunes, DateOnly Domingo)
{
    public static SemanaIso De(DateOnly fecha)
    {
        var dt = fecha.ToDateTime(TimeOnly.MinValue);
        var desdeLunes = ((int)fecha.DayOfWeek + 6) % 7;
        var lunes = fecha.AddDays(-desdeLunes);
        return new SemanaIso(
            ISOWeek.GetYear(dt), ISOWeek.GetWeekOfYear(dt), lunes, lunes.AddDays(6));
    }
}
