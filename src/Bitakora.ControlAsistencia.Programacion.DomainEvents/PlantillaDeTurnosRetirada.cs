namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class PlantillaDeTurnosRetirada
{
    public Guid PlantillaId { get; private set; }

    private PlantillaDeTurnosRetirada() { }

    public static PlantillaDeTurnosRetirada Crear(Guid plantillaId) => throw new NotImplementedException();
}
