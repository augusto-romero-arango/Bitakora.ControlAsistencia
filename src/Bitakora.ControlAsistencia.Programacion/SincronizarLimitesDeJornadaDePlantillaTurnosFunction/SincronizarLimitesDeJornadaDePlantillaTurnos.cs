using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.SincronizarLimitesDeJornadaDePlantillaTurnosFunction;

public record SincronizarLimitesDeJornadaDePlantillaTurnos(
    Guid PlantillaId, Guid JornadaId, LimitesJornada Limites, long Version);
