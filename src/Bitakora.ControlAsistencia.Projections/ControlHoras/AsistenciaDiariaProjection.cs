using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Marten.Events.Aggregation; // SingleStreamProjection<,> vive aqui, NO en Marten.Events.Projections

namespace Bitakora.ControlAsistencia.Projections.ControlHoras;

/// <summary>
/// Clase de proyeccion companion de AsistenciaDiaria (N1: un solo stream
/// "dc:{CodigoColaborador}:{yyyyMMdd}" por fila).
///
/// partial es OBLIGATORIO: el source generator descubre Create/Apply por convencion y emite el
/// dispatcher [GeneratedEvolver]. Sin partial el build queda limpio y la proyeccion falla en
/// RUNTIME al registrarse (InvalidProjectionException); lo caza el config-test
/// ConfigurarControlHoras_RegistraAsistenciaDiariaProjectionComoAsync.
///
/// Sin ShouldDelete: la fila nunca se borra.
/// </summary>
public sealed partial class AsistenciaDiariaProjection : SingleStreamProjection<AsistenciaDiaria, string>
{
    public static AsistenciaDiaria Create(DepuracionDiaRecibida evento)
    {
        var plan = ClasificarPlan(evento.NombreTurno, evento.Franjas);

        return new AsistenciaDiaria(
            evento.Id,
            evento.CodigoColaborador,
            evento.Fecha,
            EstadoAsistencia.Provisional,
            plan,
            evento.NombreTurno,
            NoSePresento: EsNoSePresento(plan, evento.Marcaciones),
            FranjasIncompletas: EsFranjasIncompletas(plan, evento.Franjas),
            VinoEnDescanso: EsVinoEnDescanso(plan, evento.Marcaciones),
            TrabajoSinProgramacion: EsTrabajoSinProgramacion(plan, evento.Marcaciones),
            evento.HorasDiscriminadas.HorasPorConcepto);
    }

    // "El ultimo gana": cada foto reemplaza plan, banderas y horas. Id/CodigoColaborador/Fecha se
    // omiten a proposito -- son la identidad del stream, invariante para todo evento del documento.
    // Estado tampoco se toca: ningun evento produce todavia un valor distinto de Provisional.
    public static AsistenciaDiaria Apply(DepuracionDiaRecibida evento, AsistenciaDiaria vista)
    {
        var plan = ClasificarPlan(evento.NombreTurno, evento.Franjas);

        return vista with
        {
            Plan = plan,
            NombreTurno = evento.NombreTurno,
            NoSePresento = EsNoSePresento(plan, evento.Marcaciones),
            FranjasIncompletas = EsFranjasIncompletas(plan, evento.Franjas),
            VinoEnDescanso = EsVinoEnDescanso(plan, evento.Marcaciones),
            TrabajoSinProgramacion = EsTrabajoSinProgramacion(plan, evento.Marcaciones),
            HorasPorConcepto = evento.HorasDiscriminadas.HorasPorConcepto
        };
    }

    private static PlanDelDia ClasificarPlan(string? nombreTurno, IReadOnlyList<FranjaDepurada> franjas) =>
        nombreTurno switch
        {
            null => PlanDelDia.SinProgramar,
            _ when franjas.Count == 0 => PlanDelDia.Descanso,
            _ => PlanDelDia.ConJornada
        };

    private static bool EsNoSePresento(PlanDelDia plan, IReadOnlyList<MarcacionDelDia> marcaciones) =>
        plan == PlanDelDia.ConJornada && marcaciones.Count == 0;

    private static bool EsFranjasIncompletas(PlanDelDia plan, IReadOnlyList<FranjaDepurada> franjas) =>
        plan == PlanDelDia.ConJornada && franjas.Any(franja => franja.EsAnomala);

    private static bool EsVinoEnDescanso(PlanDelDia plan, IReadOnlyList<MarcacionDelDia> marcaciones) =>
        plan == PlanDelDia.Descanso && marcaciones.Count > 0;

    private static bool EsTrabajoSinProgramacion(PlanDelDia plan, IReadOnlyList<MarcacionDelDia> marcaciones) =>
        plan == PlanDelDia.SinProgramar && marcaciones.Count > 0;
}
