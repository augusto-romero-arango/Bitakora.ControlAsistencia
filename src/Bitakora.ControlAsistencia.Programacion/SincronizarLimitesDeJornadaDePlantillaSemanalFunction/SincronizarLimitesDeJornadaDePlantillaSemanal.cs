using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.SincronizarLimitesDeJornadaDePlantillaSemanalFunction;

public record SincronizarLimitesDeJornadaDePlantillaSemanal(
    Guid PlantillaId, Guid JornadaId, LimitesJornada Limites, long Version);
