using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

/// <summary>
/// Clasifica un sub-intervalo homogeneo por banda horaria y tipo de dia.
/// Precondicion de ClasificarBanda: el intervalo recibido ya no cruza las fronteras 6AM ni 7PM.
/// Si se viola la precondicion, el comportamiento es indefinido.
/// </summary>
public static class ClasificadorHorario
{
    /// <summary>
    /// Diurna si la hora de inicio cae en [06:00, 19:00); Nocturna en caso contrario. Basta el
    /// inicio porque el intervalo ya es homogeneo.
    /// </summary>
    public static BandaHoraria ClasificarBanda(IntervaloTemporal intervalo)
    {
        // IntervaloTemporal no expone Inicio. La hora del inicio no depende del DiaOffset, asi que
        // una fecha ancla arbitraria basta.
        var (inicio, _) = intervalo.ResolverA(default);
        var horaInicio = TimeOnly.FromDateTime(inicio);

        return horaInicio >= FronterasHorariasLegales.InicioDiurna
               && horaInicio < FronterasHorariasLegales.InicioNocturna
            ? BandaHoraria.Diurna
            : BandaHoraria.Nocturna;
    }

    /// <summary>
    /// El tipo de dia se evalua sobre la fecha resuelta (fechaAncla + DiaOffset), no sobre el ancla:
    /// la madrugada del dia siguiente puede caer en otro tipo de dia.
    /// </summary>
    public static TipoDia ClasificarTipoDia(
        MomentoDelDia momento,
        DateOnly fechaAncla,
        Func<DateOnly, bool> esFestivo)
    {
        var fechaResuelta = fechaAncla.AddDays(momento.DiaOffset);

        // Domingo y festivo a la vez se clasifican igual, sin duplicar recargo.
        return fechaResuelta.DayOfWeek == DayOfWeek.Sunday || esFestivo(fechaResuelta)
            ? TipoDia.DominicalFestivo
            : TipoDia.Habil;
    }
}
