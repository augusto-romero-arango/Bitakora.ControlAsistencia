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
    private sealed record DiaAsignado(Guid TurnoId, Turno Turno, long VersionTurno);

    private readonly Dictionary<(int Semana, DiaSemana Dia), DiaAsignado> _dias = new();

    public void Apply(PlantillaSemanalCreada evento)
    {
        Id = evento.PlantillaId.ToString();
        _semanas = evento.Semanas;
        _estaActiva = true;
    }

    public void Apply(DiaDePlantillaSemanalAsignado evento) =>
        _dias[(evento.Semana, evento.Dia)] =
            new DiaAsignado(evento.TurnoId, evento.Turno, evento.VersionTurno);

    // Remove sobre una clave ausente devuelve false sin lanzar (MEF-ADR-0004 capa 4).
    public void Apply(DiaDePlantillaSemanalQuitado evento) => _dias.Remove((evento.Semana, evento.Dia));

    public void Apply(PlantillaSemanalRetirada evento) => _estaActiva = false;

    public void Apply(JornadaDePlantillaSemanalAsignada evento)
    {
        JornadaId = evento.JornadaId;
        Limites = evento.Limites;
        VersionJornada = evento.VersionJornada;
    }

    public void Apply(JornadaDePlantillaSemanalQuitada evento)
    {
        JornadaId = null;
        Limites = null;
        VersionJornada = 0;
    }

    internal Guid? JornadaId { get; private set; }

    internal LimitesJornada? Limites { get; private set; }

    internal long VersionJornada { get; private set; }

    // Declina con resultado (CA-ADR-0030). Precedencia: retirada > sin cambios > asignada. La copia
    // atrasada (version menor a la ofrecida) es la autocorreccion y emite el evento.
    internal ResultadoAsignarJornada AsignarJornada(Guid jornadaId, LimitesJornada limites, long version)
    {
        if (!_estaActiva)
            return ResultadoAsignarJornada.PlantillaRetirada;

        if (JornadaId == jornadaId && VersionJornada >= version)
            return ResultadoAsignarJornada.SinCambios;

        var evento = JornadaDePlantillaSemanalAsignada.Crear(Guid.Parse(Id), jornadaId, limites, version);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return ResultadoAsignarJornada.Asignada;
    }

    internal ResultadoQuitarJornada QuitarJornada()
    {
        if (!_estaActiva)
            return ResultadoQuitarJornada.PlantillaRetirada;

        if (JornadaId is null)
            return ResultadoQuitarJornada.SinCambios;

        var evento = JornadaDePlantillaSemanalQuitada.Crear(Guid.Parse(Id));
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return ResultadoQuitarJornada.Quitada;
    }

    internal static PlantillaSemanalTurnos Iniciar(PlantillaSemanalCreada evento)
    {
        var plantilla = new PlantillaSemanalTurnos();
        plantilla._uncommittedEvents.Add(evento);
        plantilla.Apply(evento);
        return plantilla;
    }

    // Precedencia: plantilla retirada > semana fuera de rango > sin cambios > asignado. Una copia
    // atrasada (version menor a la ofrecida) es la autocorreccion y emite el evento.
    internal ResultadoAsignarDia AsignarDia(int semana, DiaSemana dia, Guid turnoId, Turno copia, long versionTurno)
    {
        if (!_estaActiva)
            return ResultadoAsignarDia.PlantillaRetirada;

        if (semana > _semanas)
            return ResultadoAsignarDia.SemanaFueraDeRango;

        if (_dias.TryGetValue((semana, dia), out var actual)
            && actual.TurnoId == turnoId && actual.VersionTurno >= versionTurno)
            return ResultadoAsignarDia.SinCambios;

        var evento = DiaDePlantillaSemanalAsignado.Crear(Guid.Parse(Id), semana, dia, turnoId, copia, versionTurno);
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
