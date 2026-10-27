using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

public partial class PlantillaTurnos : AggregateRoot
{
    public void Apply(PlantillaDeTurnosCreada evento) => Id = evento.PlantillaId.ToString();

    public void Apply(DiaDePlantillaDeTurnosAsignado evento) => throw new NotImplementedException();

    internal ResultadoAsignarDiaDePlantillaTurnos AsignarDia(int dia, Guid turnoId, Turno copia, long versionTurno)
        => throw new NotImplementedException();

    public static PlantillaTurnos Iniciar(PlantillaDeTurnosCreada evento)
    {
        var plantilla = new PlantillaTurnos();
        plantilla._uncommittedEvents.Add(evento);
        plantilla.Apply(evento);
        return plantilla;
    }
}
