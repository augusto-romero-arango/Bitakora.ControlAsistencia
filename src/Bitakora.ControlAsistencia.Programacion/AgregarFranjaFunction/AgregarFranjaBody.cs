using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.AgregarFranjaFunction;

// Sin TurnoId: viaja en la ruta (programacion/turnos/{id}:agregar-franja), no en el body.
public record AgregarFranjaBody(
    TimeOnly Inicio,
    TimeOnly Fin,
    int? DiaOffsetFin = null,
    SedeProgramada? Sede = null);
