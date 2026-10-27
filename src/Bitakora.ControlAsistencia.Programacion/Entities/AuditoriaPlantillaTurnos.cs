using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

public sealed record DiaDePlantillaTurnosAuditado(int Dia, Turno Turno);

public static class AuditoriaPlantillaTurnos
{
    public static IReadOnlyList<AdvertenciaPlantillaTurnos> Auditar(
        int diasDelCiclo,
        IEnumerable<DiaDePlantillaTurnosAuditado> diasOcupados,
        LimitesJornada? limites)
    {
        if (limites is null)
            return [AdvertenciaPlantillaTurnos.PlantillaSinJornada()];

        var ocupados = diasOcupados.ToDictionary(d => d.Dia);
        return Enumerable.Range(1, diasDelCiclo)
            .SelectMany(dia => AuditarDia(
                dia,
                ocupados.TryGetValue(dia, out var d) && d.Turno.EstaCompleto() ? d.Turno : null,
                limites))
            .Order()
            .ToList()
            .AsReadOnly();
    }

    private static IEnumerable<AdvertenciaPlantillaTurnos> AuditarDia(int dia, Turno? turno, LimitesJornada limites)
    {
        if (turno is null)
            return [AdvertenciaPlantillaTurnos.DiaSinTurno(dia)];
        if (turno.EsDescanso())
            return [];

        var minutos = turno.MinutosOrdinarios();
        var advertencias = new List<AdvertenciaPlantillaTurnos>();
        var deMas = limites.MinutosDeMasEnElDia(minutos);
        if (deMas > 0)
            advertencias.Add(AdvertenciaPlantillaTurnos.SuperaTopeDiario(dia, deMas));
        var deMenos = limites.MinutosDeMenosEnElDia(minutos);
        if (deMenos > 0)
            advertencias.Add(AdvertenciaPlantillaTurnos.PorDebajoDelMinimoDiario(dia, deMenos));
        return advertencias;
    }
}
