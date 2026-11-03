using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using Marten.Events.Projections;

namespace Bitakora.ControlAsistencia.Projections.Programacion;

/// <summary>
/// Receta N2 (MEF-ADR-0035): el documento es por colaborador y reune los streams mensuales
/// bm:{codigo}:{yyyyMM}. partial es obligatorio para el source generator de JasperFx.
/// </summary>
public sealed partial class BorradorDeProgramacionProjection : MultiStreamProjection<BorradorDeProgramacion, string>
{
    private const int MinutosPorDia = 1440;

    public BorradorDeProgramacionProjection()
    {
        Identity<ProgramacionTurnoAgregadaAlBorrador>(e => e.Colaborador.CodigoColaborador);
        Identity<ProgramacionPublicada>(e => e.Colaborador.CodigoColaborador);
        Identity<BorradorDeProgramacionDescartado>(e => e.Colaborador.CodigoColaborador);
    }

    public static BorradorDeProgramacion Create(ProgramacionTurnoAgregadaAlBorrador e) =>
        new(
            e.Colaborador.CodigoColaborador,
            e.Colaborador.CodigoColaborador,
            e.Colaborador.Identificacion,
            e.Colaborador.NombreCompleto,
            CasillasDe(e).OrderBy(d => d.Fecha).ToList());

    // El ultimo gana: las fechas del evento reemplazan sus casillas. Sin documento previo el
    // evolver generado puede pasar una vista sin inicializar (Dias null); el ?? [] evita que lance.
    public static BorradorDeProgramacion Apply(ProgramacionTurnoAgregadaAlBorrador e, BorradorDeProgramacion vista)
    {
        var nuevas = CasillasDe(e);
        var reemplazadas = nuevas.Select(d => d.Fecha).ToHashSet();
        var dias = (vista.Dias ?? [])
            .Where(d => !reemplazadas.Contains(d.Fecha))
            .Concat(nuevas)
            .OrderBy(d => d.Fecha)
            .ToList();

        return vista with
        {
            Identificacion = e.Colaborador.Identificacion,
            NombreCompleto = e.Colaborador.NombreCompleto,
            Dias = dias
        };
    }

    // null borra el documento (JasperFx traduce snapshot null a Delete); Apply y ShouldDelete del
    // mismo evento no coexisten en el generador (CS8120).
    public static BorradorDeProgramacion? Apply(ProgramacionPublicada e, BorradorDeProgramacion vista) =>
        QuitarFechas(vista, e.Fechas);

    public static BorradorDeProgramacion? Apply(BorradorDeProgramacionDescartado e, BorradorDeProgramacion vista) =>
        QuitarFechas(vista, e.Fechas);

    private static BorradorDeProgramacion? QuitarFechas(BorradorDeProgramacion vista, IReadOnlyList<DateOnly> fechas)
    {
        var quitar = fechas.ToHashSet();
        var dias = (vista.Dias ?? []).Where(d => !quitar.Contains(d.Fecha)).ToList();
        return dias.Count == 0 ? null : vista with { Dias = dias };
    }

    private static List<DiaDeBorrador> CasillasDe(ProgramacionTurnoAgregadaAlBorrador e) =>
        e.Fechas.Distinct()
            .Select(fecha => new DiaDeBorrador(
                fecha,
                e.DetalleTurno.Nombre,
                e.DetalleTurno.Descripcion,
                BloquesDe(e, fecha),
                e.DetalleTurno.EsHorarioAbierto))
            .ToList();

    private static List<Bloque> BloquesDe(ProgramacionTurnoAgregadaAlBorrador e, DateOnly fecha)
    {
        var medianoche = fecha.ToDateTime(TimeOnly.MinValue);
        return e.DetalleTurno.FranjasOrdinarias
            .SelectMany(f => Recortar(f, f.Sede ?? e.Sede))
            .Select(t => new Bloque(t.Tipo, medianoche.AddMinutes(t.Inicio), medianoche.AddMinutes(t.Fin),
                t.Sede?.Id, t.Sede?.Nombre))
            .ToList();
    }

    private static IEnumerable<(TipoBloque Tipo, int Inicio, int Fin, SedeProgramada? Sede)> Recortar(
        FranjaProgramada franja, SedeProgramada? sede)
    {
        var subs = franja.Descansos
            .Select(d => (Tipo: TipoBloque.Descanso, Inicio: Minutos(d.HoraInicio, d.DiaOffsetInicio),
                Fin: Minutos(d.HoraFin, d.DiaOffsetFin)))
            .Concat(franja.Extras.Select(x => (Tipo: TipoBloque.Extra, Inicio: Minutos(x.HoraInicio, x.DiaOffsetInicio),
                Fin: Minutos(x.HoraFin, x.DiaOffsetFin))))
            .OrderBy(s => s.Inicio);

        var finFranja = Minutos(franja.HoraFin, franja.DiaOffsetFin);
        var cursor = Minutos(franja.HoraInicio, 0);
        foreach (var sub in subs)
        {
            if (sub.Inicio > cursor)
                yield return (TipoBloque.Ordinaria, cursor, sub.Inicio, sede);
            yield return (sub.Tipo, sub.Inicio, sub.Fin, sede);
            cursor = sub.Fin;
        }

        if (cursor < finFranja)
            yield return (TipoBloque.Ordinaria, cursor, finFranja, sede);
    }

    private static int Minutos(TimeOnly hora, int diaOffset) =>
        hora.Hour * 60 + hora.Minute + diaOffset * MinutosPorDia;
}
