using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

// Segundo nivel de composicion sobre el Turno (CA-ADR-0034). Nace vacia: el resto del estado
// (_nombre, _semanas, _estaActiva, _dias) entra con su primer consumidor, no antes.
// Anatomia de clave (CA-ADR-0031): Guid canonico "D", sin prefijo.
public partial class PlantillaSemanalTurnos : AggregateRoot
{
    public void Apply(PlantillaSemanalCreada evento) => Id = evento.PlantillaId.ToString();

    // Issue #621: reemplaza (o pone por primera vez) el turno de un slot (semana, dia).
    public void Apply(DiaDePlantillaSemanalAsignado evento) => throw new NotImplementedException();

    internal static PlantillaSemanalTurnos Iniciar(PlantillaSemanalCreada evento)
    {
        var plantilla = new PlantillaSemanalTurnos();
        plantilla._uncommittedEvents.Add(evento);
        plantilla.Apply(evento);
        return plantilla;
    }

    // Issue #621: CA-ADR-0030 -- declina con resultado. Precedencia: semana fuera de rango > sin
    // cambios (idempotencia) > asignado. El tope de semanas lo fijo PlantillaSemanalCreada.Semanas
    // (Apply de #620, ver Notas tecnicas del issue #621).
    internal ResultadoAsignarDia AsignarDia(int semana, DiaSemana dia, Guid turnoId) =>
        throw new NotImplementedException();
}
