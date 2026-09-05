using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

// Segundo nivel de composicion sobre el Turno (CA-ADR-0034). El estado que aun no tiene consumidor
// (_nombre) entra con el, no antes.
// Anatomia de clave (CA-ADR-0031): Guid canonico "D", sin prefijo.
public partial class PlantillaSemanalTurnos : AggregateRoot
{
    private int _semanas;
    private bool _estaActiva;
    private readonly Dictionary<(int Semana, DiaSemana Dia), Guid> _dias = new();

    public void Apply(PlantillaSemanalCreada evento)
    {
        Id = evento.PlantillaId.ToString();
        _semanas = evento.Semanas;
        _estaActiva = true;
    }

    public void Apply(DiaDePlantillaSemanalAsignado evento) =>
        _dias[(evento.Semana, evento.Dia)] = evento.TurnoId;

    // Remove sobre una clave ausente devuelve false sin lanzar (MEF-ADR-0004 capa 4).
    public void Apply(DiaDePlantillaSemanalQuitado evento) => _dias.Remove((evento.Semana, evento.Dia));

    public void Apply(PlantillaSemanalRetirada evento) => _estaActiva = false;

    internal static PlantillaSemanalTurnos Iniciar(PlantillaSemanalCreada evento)
    {
        var plantilla = new PlantillaSemanalTurnos();
        plantilla._uncommittedEvents.Add(evento);
        plantilla.Apply(evento);
        return plantilla;
    }

    // Declina con resultado, nunca lanza (CA-ADR-0030). La precedencia es parte del contrato:
    // plantilla retirada > semana fuera de rango > sin cambios (idempotencia) > asignado.
    internal ResultadoAsignarDia AsignarDia(int semana, DiaSemana dia, Guid turnoId)
    {
        if (!_estaActiva)
            return ResultadoAsignarDia.PlantillaRetirada;

        if (semana > _semanas)
            return ResultadoAsignarDia.SemanaFueraDeRango;

        if (_dias.TryGetValue((semana, dia), out var turnoActual) && turnoActual == turnoId)
            return ResultadoAsignarDia.SinCambios;

        var evento = DiaDePlantillaSemanalAsignado.Crear(Guid.Parse(Id), semana, dia, turnoId);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return ResultadoAsignarDia.Asignado;
    }

    // Declina con resultado, nunca lanza (CA-ADR-0030). La precedencia es parte del contrato:
    // plantilla retirada > semana fuera de rango > sin cambios; la semana se valida antes que el
    // estado del dia, aunque ese dia ya este vacio.
    internal ResultadoQuitarDia QuitarDia(int semana, DiaSemana dia)
    {
        if (!_estaActiva)
            return ResultadoQuitarDia.PlantillaRetirada;

        if (semana > _semanas)
            return ResultadoQuitarDia.SemanaFueraDeRango;

        if (!_dias.ContainsKey((semana, dia)))
            return ResultadoQuitarDia.SinCambios;

        var evento = DiaDePlantillaSemanalQuitado.Crear(Guid.Parse(Id), semana, dia);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return ResultadoQuitarDia.Quitado;
    }

    // Declina con resultado, nunca lanza (CA-ADR-0030). Retirar una plantilla ya retirada es
    // SinCambios (204 idempotente), no el 409 del precedente CatalogoTurnos.Retirar.
    internal ResultadoRetiroPlantilla Retirar()
    {
        if (!_estaActiva)
            return ResultadoRetiroPlantilla.SinCambios;

        var evento = PlantillaSemanalRetirada.Crear(Guid.Parse(Id));
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return ResultadoRetiroPlantilla.Retirada;
    }
}
