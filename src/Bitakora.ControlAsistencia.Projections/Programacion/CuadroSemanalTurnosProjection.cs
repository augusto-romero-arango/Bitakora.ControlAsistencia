using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using JasperFx.Events; // IEvent<T> vive aqui, NO en Marten.Events (MEF-ADR-0034 seccion 6)
using Marten.Events.Aggregation; // SingleStreamProjection<,> vive aqui, NO en Marten.Events.Projections

namespace Bitakora.ControlAsistencia.Projections.Programacion;

/// <summary>
/// Clase de proyeccion companion de CuadroSemanalTurnos (receta N1: stream de la plantilla).
/// partial es obligatorio (source generator de Marten).
/// </summary>
public sealed partial class CuadroSemanalTurnosProjection
    : SingleStreamProjection<CuadroSemanalTurnos, string>
{
    private const int DiasPorSemana = 7;
    private const int MinutosPorHora = 60;

    public static CuadroSemanalTurnos Create(IEvent<PlantillaSemanalCreada> e) =>
        new(e.StreamKey!, e.Data.Nombre, e.Data.Semanas, [], false, null, null, []);

    public static CuadroSemanalTurnos Apply(DiaDePlantillaSemanalAsignado e, CuadroSemanalTurnos vista)
    {
        var dia = new DiaDelCuadro(
            e.Semana, e.Dia.Numero, e.TurnoId.ToString(), e.Turno.Nombre,
            e.Turno.ToString(), e.Turno.EstaCompleto(), false);
        var dias = vista.Dias
            .Where(d => !(d.Semana == e.Semana && d.Dia == e.Dia.Numero))
            .Append(dia)
            .OrderBy(d => d.Semana).ThenBy(d => d.Dia)
            .ToList();
        return ConDias(vista, dias);
    }

    public static CuadroSemanalTurnos Apply(DiaDePlantillaSemanalQuitado e, CuadroSemanalTurnos vista) =>
        ConDias(vista, vista.Dias.Where(d => !(d.Semana == e.Semana && d.Dia == e.Dia.Numero)).ToList());

    public static CuadroSemanalTurnos Apply(TurnoDePlantillaSemanalSincronizado e, CuadroSemanalTurnos vista)
    {
        var turnoId = e.TurnoId.ToString();
        var dias = vista.Dias
            .Select(d => d.TurnoId != turnoId
                ? d
                : d with
                {
                    Nombre = e.Turno.Nombre,
                    Descripcion = e.Turno.ToString(),
                    Completo = e.Turno.EstaCompleto(),
                    Retirado = e.Retirado
                })
            .ToList();
        return ConDias(vista, dias);
    }

    public static CuadroSemanalTurnos Apply(JornadaDePlantillaSemanalAsignada e, CuadroSemanalTurnos vista) =>
        vista with { JornadaId = e.JornadaId, Limites = ALimites(e.Limites) };

    public static CuadroSemanalTurnos Apply(JornadaDePlantillaSemanalQuitada e, CuadroSemanalTurnos vista) =>
        vista with { JornadaId = null, Limites = null };

    public static CuadroSemanalTurnos Apply(
        LimitesDeJornadaDePlantillaSemanalSincronizados e, CuadroSemanalTurnos vista) =>
        vista with { Limites = ALimites(e.Limites) };

    public static CuadroSemanalTurnos Apply(
        AdvertenciasDePlantillaSemanalCalculadas e, CuadroSemanalTurnos vista) =>
        vista with
        {
            Advertencias = e.Advertencias
                .Select(a => new AdvertenciaDelCuadro(a.Tipo.ToString(), a.Semana, a.Dia, a.Magnitud))
                .ToList()
        };

    public static bool ShouldDelete(PlantillaSemanalRetirada e) => true;

    private static CuadroSemanalTurnos ConDias(CuadroSemanalTurnos vista, IReadOnlyList<DiaDelCuadro> dias) =>
        vista with
        {
            Dias = dias,
            Completa = dias.Count == DiasPorSemana * vista.Semanas && dias.All(d => d.Completo && !d.Retirado)
        };

    private static LimitesDelCuadro ALimites(LimitesJornada l) => new(
        EnMinutos(l.HorasSemanales), EnMinutos(l.TopeDiario), EnMinutos(l.MinimoDiario),
        l.DiasDescansoPorSemana, l.ToString());

    private static int EnMinutos(HorasYMinutos t) => t.Horas * MinutosPorHora + t.Minutos;
}
