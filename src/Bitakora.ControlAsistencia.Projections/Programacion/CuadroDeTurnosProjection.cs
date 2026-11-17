using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using JasperFx.Events;
using Marten.Events.Aggregation;

namespace Bitakora.ControlAsistencia.Projections.Programacion;

/// <summary>Clase de proyeccion companion de CuadroDeTurnos (receta N1).</summary>
public sealed partial class CuadroDeTurnosProjection
    : SingleStreamProjection<CuadroDeTurnos, string>
{
    private const int MinutosPorHora = 60;

    public static CuadroDeTurnos Create(IEvent<PlantillaDeTurnosCreada> e) =>
        new(e.StreamKey!, e.Data.Nombre, e.Data.DiasDelCiclo, [], false, null);

    public static CuadroDeTurnos Apply(DiaDePlantillaDeTurnosAsignado e, CuadroDeTurnos vista)
    {
        var dia = new DiaDelCuadroDeTurnos(
            e.Dia, e.TurnoId.ToString(), e.Turno.Nombre,
            e.Turno.ToString(), e.Turno.EstaCompleto(), false,
            e.Turno.EsDescanso(), HorarioAbiertoDe(e.Turno));
        var dias = vista.Dias
            .Where(d => d.Dia != e.Dia)
            .Append(dia)
            .OrderBy(d => d.Dia)
            .ToList();
        return vista with { Dias = dias, Completa = EstaCompleta(dias, vista.DiasDelCiclo) };
    }

    public static CuadroDeTurnos Apply(TurnoDePlantillaDeTurnosSincronizado e, CuadroDeTurnos vista)
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
                    Retirado = e.Retirado,
                    EsDescanso = e.Turno.EsDescanso(),
                    HorarioAbierto = HorarioAbiertoDe(e.Turno)
                })
            .ToList();
        return vista with { Dias = dias, Completa = EstaCompleta(dias, vista.DiasDelCiclo) };
    }

    public static CuadroDeTurnos Apply(JornadaDePlantillaDeTurnosAsignada e, CuadroDeTurnos vista) =>
        vista with { JornadaId = e.JornadaId };

    public static CuadroDeTurnos Apply(JornadaDePlantillaDeTurnosQuitada e, CuadroDeTurnos vista) =>
        vista with { JornadaId = null };

    public static bool ShouldDelete(PlantillaDeTurnosRetirada e) => true;

    private static HorarioAbiertoDelCuadro? HorarioAbiertoDe(Turno turno) =>
        !turno.EsDescanso() && turno.EsHorarioAbierto()
            ? new HorarioAbiertoDelCuadro(FormatearDuracion(turno.MinutosOrdinarios()))
            : null;

    private static string FormatearDuracion(int minutos) =>
        HorasYMinutos.Crear(minutos / MinutosPorHora, minutos % MinutosPorHora).ToString();

    private static bool EstaCompleta(IReadOnlyCollection<DiaDelCuadroDeTurnos> dias, int diasDelCiclo) =>
        dias.Count == diasDelCiclo && dias.All(d => d.Completo && !d.Retirado);
}
