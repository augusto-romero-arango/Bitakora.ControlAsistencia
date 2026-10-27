using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

public partial class PlantillaTurnos : AggregateRoot
{
    public void Apply(PlantillaDeTurnosCreada evento) => throw new NotImplementedException();

    public static PlantillaTurnos Iniciar(PlantillaDeTurnosCreada evento) => throw new NotImplementedException();
}
