// Issue #134: Clasificar segmentos horarios por banda y tipo de dia
// Capa 2 del calculo de desglose de horas.
// Clasifica dos ejes independientes: banda horaria (diurna/nocturna) y tipo de dia (habil/dominicalFestivo).
// Los umbrales de banda NO se duplican aqui - se consumen de FronterasHorariasLegales (#115).
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

/// <summary>
/// Clasifica un sub-intervalo homogeneo por banda horaria y tipo de dia.
/// Precondicion de ClasificarBanda: el intervalo recibido ya no cruza las fronteras 6AM ni 7PM.
/// Si se viola la precondicion, el comportamiento es indefinido.
/// </summary>
public static class ClasificadorHorario
{
    /// <summary>
    /// Clasifica la banda horaria de un sub-intervalo homogeneo.
    /// Solo necesita el momento de inicio porque el intervalo ya es homogeneo.
    /// Retorna Diurna si inicio.Hora in [06:00, 19:00); Nocturna en caso contrario.
    /// </summary>
    public static BandaHoraria ClasificarBanda(IntervaloTemporal intervalo)
    {
        // IntervaloTemporal encapsula sus extremos (PR #148, Tell-don't-Ask): no expone Inicio.
        // La banda depende solo de la hora-del-dia del inicio, que es invariante al DiaOffset,
        // por eso resolvemos con una fecha ancla arbitraria y leemos unicamente la hora.
        var (inicio, _) = intervalo.ResolverA(default);
        var horaInicio = TimeOnly.FromDateTime(inicio);

        return horaInicio >= FronterasHorariasLegales.InicioDiurna
               && horaInicio < FronterasHorariasLegales.InicioNocturna
            ? BandaHoraria.Diurna
            : BandaHoraria.Nocturna;
    }

    /// <summary>
    /// Clasifica el tipo de dia del momento dado.
    /// Resuelve la fecha real como fechaAncla.AddDays(momento.DiaOffset) y consulta esFestivo.
    /// Retorna DominicalFestivo si DayOfWeek == Sunday o esFestivo(fechaResuelta); Habil en caso contrario.
    /// </summary>
    public static TipoDia ClasificarTipoDia(
        MomentoDelDia momento,
        DateOnly fechaAncla,
        Func<DateOnly, bool> esFestivo)
    {
        // El DiaOffset puede empujar el momento a un dia distinto al ancla (madrugada del dia
        // siguiente). El tipo de dia se evalua sobre la fecha resuelta, no sobre el ancla.
        var fechaResuelta = fechaAncla.AddDays(momento.DiaOffset);

        // OR inclusivo: domingo o festivo se clasifican igual (DominicalFestivo) sin duplicar recargo.
        return fechaResuelta.DayOfWeek == DayOfWeek.Sunday || esFestivo(fechaResuelta)
            ? TipoDia.DominicalFestivo
            : TipoDia.Habil;
    }
}
