using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.AgregarFranjaFunction;

// Comando interno: el endpoint lo compone desde el {id} de la ruta mas el body.
public record AgregarFranja(
    Guid TurnoId,
    TimeOnly Inicio,
    TimeOnly Fin,
    int? DiaOffsetFin = null,
    SedeProgramada? Sede = null);
