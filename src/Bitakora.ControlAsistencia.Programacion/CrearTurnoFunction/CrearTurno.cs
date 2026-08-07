using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;

// Issue #3: comando DTO para crear un turno de trabajo
// ADR-0015: record = DTO sin invariantes, constructor primario publico
public record CrearTurno(
    Guid TurnoId,
    string Nombre,
    List<CrearTurno.Franja> Ordinarias)
{
    // CA-1: record anidado con las sub-franjas del turno
    // Issue #335: Sede es opcional -- prearma la sede de esta franja en el catalogo (ver
    // ToDatosFranjas(), que la propaga a DatosFranja).
    public record Franja(
        TimeOnly Inicio,
        TimeOnly Fin,
        List<(TimeOnly inicio, TimeOnly fin)> Descansos,
        List<(TimeOnly inicio, TimeOnly fin)> Extras,
        SedeProgramada? Sede = null);

    // Issue #237: el contrato HTTP se queda aqui y se traduce a la entrada del factory de
    // TurnoCreado. Un solo lugar con el mapeo: lo reusan el handler y sus tests.
    // Issue #335 CA-1: propaga o.Sede -- prearma la sede de la franja del catalogo.
    public List<DatosFranja> ToDatosFranjas() =>
        Ordinarias
            .Select(o => new DatosFranja(o.Inicio, o.Fin, o.Descansos, o.Extras, o.Sede))
            .ToList();
}
