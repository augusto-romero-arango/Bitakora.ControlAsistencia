using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Entities;

internal static class AdvertenciasEsperadasPlantillaTurnos
{
    internal static AdvertenciaPlantillaTurnos[] SoloSinJornada =>
        [AdvertenciaPlantillaTurnos.PlantillaSinJornada()];

    internal static AdvertenciaPlantillaTurnos[] DiasSinTurno(int dias) =>
        Enumerable.Range(1, dias).Select(AdvertenciaPlantillaTurnos.DiaSinTurno).ToArray();

    internal static AdvertenciasDePlantillaDeTurnosCalculadas EventoSinJornada(Guid plantillaId) =>
        AdvertenciasDePlantillaDeTurnosCalculadas.Crear(plantillaId, SoloSinJornada);

    internal static AdvertenciasDePlantillaDeTurnosCalculadas EventoDiasSinTurno(Guid plantillaId, int dias) =>
        AdvertenciasDePlantillaDeTurnosCalculadas.Crear(plantillaId, DiasSinTurno(dias));
}
