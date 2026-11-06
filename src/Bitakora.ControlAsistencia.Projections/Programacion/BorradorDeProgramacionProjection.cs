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

    // La sede de programacion rellena las franjas sin sede; la aritmetica de bloques vive en
    // TurnoProgramado.Segmentar (MEF-ADR-0012).
    private static List<DiaDeBorrador> CasillasDe(ProgramacionTurnoAgregadaAlBorrador e)
    {
        var turno = e.DetalleTurno.ConSedePorDefecto(e.Sede);
        return e.Fechas.Distinct()
            .Select(fecha => new DiaDeBorrador(
                fecha,
                turno.Nombre,
                turno.Descripcion,
                turno.Segmentar(fecha).Select(MapearBloque).ToList(),
                turno.EsHorarioAbierto))
            .ToList();
    }

    private static readonly Dictionary<TipoBloqueProgramado, TipoBloque> Tipos = new()
    {
        [TipoBloqueProgramado.Ordinaria] = TipoBloque.Ordinaria,
        [TipoBloqueProgramado.Descanso] = TipoBloque.Descanso,
        [TipoBloqueProgramado.Extra] = TipoBloque.Extra
    };

    private static Bloque MapearBloque(BloqueProgramado bloque) =>
        new(Tipos[bloque.Tipo], bloque.Inicio, bloque.Fin, bloque.Sede?.Id, bloque.Sede?.Nombre);
}
