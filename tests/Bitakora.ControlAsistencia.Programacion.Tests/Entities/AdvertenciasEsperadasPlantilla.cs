using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Entities;

// Oraculo escrito a mano, no derivado de AuditoriaPlantillaSemanal: supone una Jornada con tope
// diario de 8 h y 1 dia de descanso por semana.
internal static class AdvertenciasEsperadasPlantilla
{
    internal static AdvertenciaPlantillaSemanal[] SoloSinJornada =>
        [AdvertenciaPlantillaSemanal.PlantillaSinJornada()];

    internal static AdvertenciasDePlantillaSemanalCalculadas SinJornada(Guid plantillaId) =>
        AdvertenciasDePlantillaSemanalCalculadas.Crear(plantillaId, SoloSinJornada);

    internal static IEnumerable<AdvertenciaPlantillaSemanal> SemanaVacia(int semana, int horasSemanales) =>
        new[]
        {
            AdvertenciaPlantillaSemanal.PorDebajoDeHorasSemanales(semana, horasSemanales * 60),
            AdvertenciaPlantillaSemanal.FaltanDiasDeDescanso(semana, 1)
        }.Concat(Enumerable.Range(1, 7)
            .Select(d => AdvertenciaPlantillaSemanal.DiaSinTurno(semana, DiaSemana.Desde(d))));

    internal static AdvertenciaPlantillaSemanal[] PlantillaVaciaConJornada(int semanas, int horasSemanales) =>
        Enumerable.Range(1, semanas).SelectMany(s => SemanaVacia(s, horasSemanales)).ToArray();
}
