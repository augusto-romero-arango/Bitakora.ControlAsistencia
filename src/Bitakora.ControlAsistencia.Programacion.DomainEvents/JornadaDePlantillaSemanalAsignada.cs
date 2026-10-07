namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class JornadaDePlantillaSemanalAsignada
{
    public Guid PlantillaId { get; private set; }
    public Guid JornadaId { get; private set; }
    public LimitesJornada Limites { get; private set; } = null!;
    public long VersionJornada { get; private set; }

    private JornadaDePlantillaSemanalAsignada() { }

    private JornadaDePlantillaSemanalAsignada(Guid plantillaId, Guid jornadaId, LimitesJornada limites, long versionJornada)
    {
        PlantillaId = plantillaId;
        JornadaId = jornadaId;
        Limites = limites;
        VersionJornada = versionJornada;
    }

    public static JornadaDePlantillaSemanalAsignada Crear(
        Guid plantillaId, Guid jornadaId, LimitesJornada limites, long versionJornada) =>
        new(plantillaId, jornadaId, limites, versionJornada);
}
