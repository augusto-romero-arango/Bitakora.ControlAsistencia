namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class LimitesDeJornadaDePlantillaSemanalSincronizados
{
    public Guid PlantillaId { get; private set; }
    public Guid JornadaId { get; private set; }
    public LimitesJornada Limites { get; private set; } = null!;
    public long VersionJornada { get; private set; }

    private LimitesDeJornadaDePlantillaSemanalSincronizados() { }

    public static LimitesDeJornadaDePlantillaSemanalSincronizados Crear(
        Guid plantillaId, Guid jornadaId, LimitesJornada limites, long versionJornada) =>
        new() { PlantillaId = plantillaId, JornadaId = jornadaId, Limites = limites, VersionJornada = versionJornada };
}
