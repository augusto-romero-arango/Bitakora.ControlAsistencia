using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

// Segundo nivel de composicion sobre el Turno (CA-ADR-0034). El estado que aun no tiene consumidor
// (_nombre, _estaActiva) entra con el, no antes.
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

    public void Apply(DiaDePlantillaSemanalAsignado evento) =>
        _dias[(evento.Semana, evento.Dia)] = evento.TurnoId;

    internal static PlantillaSemanalTurnos Iniciar(PlantillaSemanalCreada evento)
    {
        var plantilla = new PlantillaSemanalTurnos();
        plantilla._uncommittedEvents.Add(evento);
        plantilla.Apply(evento);
        return plantilla;
    }

    // Declina con resultado, nunca lanza (CA-ADR-0030). La precedencia es parte del contrato:
    // semana fuera de rango > sin cambios (idempotencia) > asignado.
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
