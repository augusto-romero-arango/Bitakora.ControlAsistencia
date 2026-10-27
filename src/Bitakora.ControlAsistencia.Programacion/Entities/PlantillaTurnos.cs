using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

public partial class PlantillaTurnos : AggregateRoot
{
    private sealed record DiaAsignado(Guid TurnoId, Turno Turno, long VersionTurno);

    private int _diasDelCiclo;
    private readonly Dictionary<int, DiaAsignado> _dias = new();

    public void Apply(PlantillaDeTurnosCreada evento)
    {
        Id = evento.PlantillaId.ToString();
        _diasDelCiclo = evento.DiasDelCiclo;
    }

    public void Apply(DiaDePlantillaDeTurnosAsignado evento) =>
        _dias[evento.Dia] = new DiaAsignado(evento.TurnoId, evento.Turno, evento.VersionTurno);

    // Mismo turno con copia atrasada no es no-op: reasignar autocorrige la copia (CA-ADR-0034, enmienda #886).
    internal ResultadoAsignarDiaDePlantillaTurnos AsignarDia(int dia, Guid turnoId, Turno copia, long versionTurno)
    {
        if (dia > _diasDelCiclo)
            return ResultadoAsignarDiaDePlantillaTurnos.DiaFueraDelCiclo;

        if (_dias.TryGetValue(dia, out var actual)
            && actual.TurnoId == turnoId && actual.VersionTurno >= versionTurno)
            return ResultadoAsignarDiaDePlantillaTurnos.SinCambios;

        var evento = DiaDePlantillaDeTurnosAsignado.Crear(Guid.Parse(Id), dia, turnoId, copia, versionTurno);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return ResultadoAsignarDiaDePlantillaTurnos.Asignado;
    }

    public static PlantillaTurnos Iniciar(PlantillaDeTurnosCreada evento)
    {
        var plantilla = new PlantillaTurnos();
        plantilla._uncommittedEvents.Add(evento);
        plantilla.Apply(evento);
        return plantilla;
    }
}
