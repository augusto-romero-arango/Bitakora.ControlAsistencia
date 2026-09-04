using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;

// Issue #3: comando DTO para crear un turno de trabajo
// ADR-0015: record = DTO sin invariantes, constructor primario publico
public record CrearTurno(
    Guid TurnoId,
    string Nombre,
    List<CrearTurno.Franja>? Ordinarias = null,
    bool EsDescanso = false)
{
    // CA-1: record anidado con las sub-franjas del turno
    // Issue #335: Sede es opcional -- prearma la sede de esta franja en el catalogo (ver
    // ToDatosFranjas(), que la propaga a DatosFranja).
    // Issue #601: Descansos/Extras via Rango (STJ no serializa campos de ValueTuple por defecto --
    // con tuplas, un body con hijas no vacias deserializaba en silencio a 00:00-00:00). Sus offsets
    // no son entrada: se infieren en el dominio (#600). DiaOffsetFin opcional -- ausente/0 = inferir,
    // 1 explicito habilita la franja de 24 h exactas.
    public record Franja(
        TimeOnly Inicio,
        TimeOnly Fin,
        List<Rango>? Descansos = null,
        List<Rango>? Extras = null,
        SedeProgramada? Sede = null,
        int? DiaOffsetFin = null);

    // Issue #601: rango horario de una hija del contrato HTTP. Nombre distinto de SubFranja
    // (Programacion.DomainEvents) a proposito: un using equivocado ahi compilaria contra el VO.
    public record Rango(TimeOnly Inicio, TimeOnly Fin);

    // Issue #237: el contrato HTTP se queda aqui y se traduce a la entrada del factory de
    // TurnoCreado. Un solo lugar con el mapeo: lo reusan el handler y sus tests.
    // Issue #335 CA-1: propaga o.Sede -- prearma la sede de la franja del catalogo.
    // Issue #601: mapea cada Rango a la tupla que DatosFranja espera y propaga DiaOffsetFin.
    public List<DatosFranja> ToDatosFranjas() => throw new NotImplementedException();
}
