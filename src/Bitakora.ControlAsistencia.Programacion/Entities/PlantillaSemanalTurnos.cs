using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

// Segundo nivel de composicion sobre el Turno (CA-ADR-0034). Nace vacia: el resto del estado
// (_nombre, _semanas, _estaActiva, _dias) entra con su primer consumidor, no antes.
// Anatomia de clave (CA-ADR-0031): Guid canonico "D", sin prefijo.
public class PlantillaSemanalTurnos : AggregateRoot
{
    public void Apply(PlantillaSemanalCreada evento) => Id = evento.PlantillaId.ToString();

    internal static PlantillaSemanalTurnos Iniciar(PlantillaSemanalCreada evento)
    {
        var plantilla = new PlantillaSemanalTurnos();
        plantilla._uncommittedEvents.Add(evento);
        plantilla.Apply(evento);
        return plantilla;
    }
}
