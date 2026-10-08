namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Forma minima para compilar los tests read-side de #882; #884 es su dueno (persistencia, alias,
// serializacion y emision) y puede reajustar su forma.
public sealed class LimitesDeJornadaDePlantillaSemanalSincronizados
{
    public Guid PlantillaId { get; private set; }
    public LimitesJornada Limites { get; private set; } = null!;
    public long VersionJornada { get; private set; }

    private LimitesDeJornadaDePlantillaSemanalSincronizados() { }

    public static LimitesDeJornadaDePlantillaSemanalSincronizados Crear(
        Guid plantillaId, LimitesJornada limites, long versionJornada) =>
        new() { PlantillaId = plantillaId, Limites = limites, VersionJornada = versionJornada };
}
