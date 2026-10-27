using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

public partial class PlantillaTurnos : AggregateRoot
{
    public void Apply(PlantillaDeTurnosCreada evento) => Id = evento.PlantillaId.ToString();

    public static PlantillaTurnos Iniciar(PlantillaDeTurnosCreada evento)
    {
        var plantilla = new PlantillaTurnos();
        plantilla._uncommittedEvents.Add(evento);
        plantilla.Apply(evento);
        return plantilla;
    }
}
