namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class TurnoDePlantillaDeTurnosSincronizado
{
    public Guid PlantillaId { get; private set; }
    public Guid TurnoId { get; private set; }
    public Turno Turno { get; private set; } = null!;
    public long VersionTurno { get; private set; }
    public bool Retirado { get; private set; }

    private TurnoDePlantillaDeTurnosSincronizado() { }

    public static TurnoDePlantillaDeTurnosSincronizado Crear(
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
