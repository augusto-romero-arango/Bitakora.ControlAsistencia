using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

// Issue #620: aggregate root de la plantilla semanal de turnos (CA-ADR-0034), segundo nivel de
// composicion sobre el Turno. Nace vacia -- este issue solo fija Id; #621-#623 agregan el resto
// del estado (_nombre, _semanas, _estaActiva, _dias) junto con su primer consumidor.
// Anatomia de clave (CA-ADR-0031): Guid canonico "D", sin prefijo -- mismo caso que CatalogoTurnos.
public partial class PlantillaSemanalTurnos : AggregateRoot
{
    // CA-3: aplica PlantillaSemanalCreada y establece Id (heredado de AggregateRoot).
    public void Apply(PlantillaSemanalCreada evento) => throw new NotImplementedException();
}
