namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class DiaDePlantillaDeTurnosAsignado
{
    public Guid PlantillaId { get; private set; }
    public int Dia { get; private set; }
    public Guid TurnoId { get; private set; }
    public Turno Turno { get; private set; } = Turno.Crear(string.Empty, false, []);
    public long VersionTurno { get; private set; }

    private DiaDePlantillaDeTurnosAsignado()
    {
    }

    public static DiaDePlantillaDeTurnosAsignado Crear(
        Guid plantillaId, int dia, Guid turnoId, Turno turno, long versionTurno)
        => throw new NotImplementedException();
}
