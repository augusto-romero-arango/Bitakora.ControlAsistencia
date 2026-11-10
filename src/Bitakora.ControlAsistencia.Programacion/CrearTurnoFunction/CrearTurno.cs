using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;

public record CrearTurno(
    Guid TurnoId,
    string Nombre,
    List<CrearTurno.Franja>? Ordinarias = null,
    bool EsDescanso = false)
{
    // Nombre distinto de SubFranja (Programacion.DomainEvents) a proposito: un using equivocado
    // ahi compilaria contra el VO. No lleva offsets: el dominio los infiere desde su ordinaria.
    public record Rango(TimeOnly Inicio, TimeOnly Fin);

    // Descansos/Extras NO pueden volver a ser tuplas: STJ no serializa campos de ValueTuple por
    // defecto, asi que un body con hijas no vacias deserializa en silencio a 00:00-00:00.
    // DiaOffsetFin ausente/0 = inferir (+1 si Fin < Inicio); 1 explicito habilita las 24 h exactas.
    public record Franja(
        TimeOnly Inicio,
        TimeOnly Fin,
        List<Rango>? Descansos = null,
        List<Rango>? Extras = null,
        SedeProgramada? Sede = null,
        int? DiaOffsetFin = null);

    // Unico lugar del mapeo del contrato HTTP a la entrada del factory de TurnoCreado.
    public List<DatosFranja> ToDatosFranjas() => (Ordinarias ?? []).Select(o => new DatosFranja(
        o.Inicio, o.Fin,
        (o.Descansos ?? []).Select(r => (r.Inicio, r.Fin)).ToList(),
        (o.Extras ?? []).Select(r => (r.Inicio, r.Fin)).ToList(),
        o.Sede,
        o.DiaOffsetFin ?? 0)).ToList();
}
