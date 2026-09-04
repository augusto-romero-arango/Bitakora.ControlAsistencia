using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.AgregarFranjaFunction;

// Comando interno: el endpoint lo compone desde el {id} de la ruta mas el body
// (MEF-ADR-0043 paso 4 -- accion de negocio con verbo propio, ni crea ni reemplaza ni remueve).
public record AgregarFranja(
    Guid TurnoId,
    TimeOnly Inicio,
    TimeOnly Fin,
    int? DiaOffsetFin = null,
    SedeProgramada? Sede = null);
