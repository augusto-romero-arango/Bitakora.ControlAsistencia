using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.AgregarFranjaFunction;

public record AgregarFranja(
    Guid TurnoId,
    TimeOnly Inicio,
    TimeOnly Fin,
    int? DiaOffsetFin = null,
    SedeProgramada? Sede = null);
