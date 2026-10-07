using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

public sealed record DiaDePlantillaAuditado(int Semana, DiaSemana Dia, Turno Turno, bool Retirado);

public static class AuditoriaPlantillaSemanal
{
    private const int DiasPorSemana = 7;

    public static IReadOnlyList<AdvertenciaPlantillaSemanal> Auditar(
        int semanas,
        IEnumerable<DiaDePlantillaAuditado> diasOcupados,
        LimitesJornada? limites)
    {
        if (limites is null)
            return [AdvertenciaPlantillaSemanal.PlantillaSinJornada()];

        var ocupados = diasOcupados.ToDictionary(d => (d.Semana, d.Dia.Numero));
        return Enumerable.Range(1, semanas)
            .SelectMany(semana => AuditarSemana(semana, ocupados, limites))
            .Order()
            .ToList()
            .AsReadOnly();
    }

    private static IEnumerable<AdvertenciaPlantillaSemanal> AuditarSemana(
        int semana,
        IReadOnlyDictionary<(int, int), DiaDePlantillaAuditado> ocupados,
        LimitesJornada limites)
    {
        // Vacio, retirado o incompleto: el dia queda sin turno (DiaSinTurno, 0 minutos, no es descanso).
        var dias = Enumerable.Range(1, DiasPorSemana)
            .Select(n => (
                Dia: DiaSemana.Desde(n),
                Turno: ocupados.TryGetValue((semana, n), out var d) && !d.Retirado && d.Turno.EstaCompleto()
                    ? d.Turno
                    : null))
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
        var diferenciaDescansos = limites.DiferenciaDeDescansos(descansos);
        if (diferenciaDescansos > 0)
            advertencias.Add(AdvertenciaPlantillaSemanal.SobranDiasDeDescanso(semana, diferenciaDescansos));
        if (diferenciaDescansos < 0)
            advertencias.Add(AdvertenciaPlantillaSemanal.FaltanDiasDeDescanso(semana, -diferenciaDescansos));

        return advertencias;
    }
}
