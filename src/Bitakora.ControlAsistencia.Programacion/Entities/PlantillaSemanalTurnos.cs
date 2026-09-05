using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

// Segundo nivel de composicion sobre el Turno (CA-ADR-0034). Nace vacia: el resto del estado
// (_nombre, _semanas, _estaActiva, _dias) entra con su primer consumidor, no antes.
// Anatomia de clave (CA-ADR-0031): Guid canonico "D", sin prefijo.
public partial class PlantillaSemanalTurnos : AggregateRoot
{
    private int _semanas;
    private readonly Dictionary<(int Semana, DiaSemana Dia), Guid> _dias = new();

    public void Apply(PlantillaSemanalCreada evento)
    {
        Id = evento.PlantillaId.ToString();
        _semanas = evento.Semanas;
    }

    // Issue #621: reemplaza (o pone por primera vez) el turno de un slot (semana, dia).
    public void Apply(DiaDePlantillaSemanalAsignado evento) =>
        _dias[(evento.Semana, evento.Dia)] = evento.TurnoId;

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
    internal ResultadoAsignarDia AsignarDia(int semana, DiaSemana dia, Guid turnoId)
    {
        if (semana > _semanas)
            return ResultadoAsignarDia.SemanaFueraDeRango;

        if (_dias.TryGetValue((semana, dia), out var turnoActual) && turnoActual == turnoId)
            return ResultadoAsignarDia.SinCambios;

        var evento = DiaDePlantillaSemanalAsignado.Crear(Guid.Parse(Id), semana, dia, turnoId);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return ResultadoAsignarDia.Asignado;
    }
}
