using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

public sealed record DiaDePlantillaAuditado(int Semana, DiaSemana Dia, Turno Turno, bool Retirado);

public static class AuditoriaPlantillaSemanal
{
    public static IReadOnlyList<AdvertenciaPlantillaSemanal> Auditar(
        int semanas,
        IEnumerable<DiaDePlantillaAuditado> diasOcupados,
        LimitesJornada? limites)
    {
        if (limites is null)
            return [AdvertenciaPlantillaSemanal.PlantillaSinJornada()];

        var ocupados = diasOcupados.ToDictionary(d => (d.Semana, d.Dia.Numero));
        var advertencias = Enumerable.Range(1, semanas)
            .SelectMany(semana => AuditarSemana(semana, ocupados, limites));

        return advertencias.OrderBy(a => a.ClaveDeOrden()).ToList().AsReadOnly();
    }

    private static IEnumerable<AdvertenciaPlantillaSemanal> AuditarSemana(
        int semana,
        IReadOnlyDictionary<(int, int), DiaDePlantillaAuditado> ocupados,
        LimitesJornada limites)
    {
        var dias = Enumerable.Range(1, 7)
            .Select(n => ocupados.TryGetValue((semana, n), out var d) && !d.Retirado && d.Turno.EstaCompleto()
                ? d.Turno : null)
            .Select((turno, i) => (Dia: DiaSemana.Desde(i + 1), Turno: turno))
            .ToList();

        var advertencias = new List<AdvertenciaPlantillaSemanal>();
        foreach (var (dia, turno) in dias)
        {
            if (turno is null)
            {
                advertencias.Add(AdvertenciaPlantillaSemanal.DiaSinTurno(semana, dia));
                continue;
            }
            if (turno.EsDescanso()) continue;

            var minutos = turno.MinutosOrdinarios();
            var deMas = limites.MinutosDeMasEnElDia(minutos);
            if (deMas > 0)
                advertencias.Add(AdvertenciaPlantillaSemanal.SuperaTopeDiario(semana, dia, deMas));
            var deMenos = limites.MinutosDeMenosEnElDia(minutos);
            if (deMenos > 0)
                advertencias.Add(AdvertenciaPlantillaSemanal.PorDebajoDelMinimoDiario(semana, dia, deMenos));
        }

        var total = dias.Sum(d => d.Turno?.MinutosOrdinarios() ?? 0);
        var diferencia = limites.DiferenciaSemanalEnMinutos(total);
        if (diferencia > 0)
            advertencias.Add(AdvertenciaPlantillaSemanal.SuperaHorasSemanales(semana, diferencia));
        if (diferencia < 0)
            advertencias.Add(AdvertenciaPlantillaSemanal.PorDebajoDeHorasSemanales(semana, -diferencia));

        var descansos = dias.Count(d => d.Turno?.EsDescanso() == true);
        var difDescansos = limites.DiferenciaDeDescansos(descansos);
        if (difDescansos > 0)
            advertencias.Add(AdvertenciaPlantillaSemanal.SobranDiasDeDescanso(semana, difDescansos));
        if (difDescansos < 0)
            advertencias.Add(AdvertenciaPlantillaSemanal.FaltanDiasDeDescanso(semana, -difDescansos));

        return advertencias;
    }
}
