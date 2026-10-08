namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Forma minima para compilar los tests read-side de #882; #883 es su dueno (persistencia, alias,
// serializacion y emision) y puede reajustar su forma.
public sealed class TurnoDePlantillaSemanalSincronizado
{
    public Guid PlantillaId { get; private set; }
    public Guid TurnoId { get; private set; }
    public Turno Turno { get; private set; } = null!;
    public long VersionTurno { get; private set; }
    public bool Retirado { get; private set; }

    private TurnoDePlantillaSemanalSincronizado() { }

    public static TurnoDePlantillaSemanalSincronizado Crear(
        Guid plantillaId, Guid turnoId, Turno turno, long versionTurno, bool retirado) =>
        new()
        {
            PlantillaId = plantillaId,
            TurnoId = turnoId,
            Turno = turno,
            VersionTurno = versionTurno,
            Retirado = retirado
        };
}
