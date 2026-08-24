using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Marten.Events.Aggregation; // SingleStreamProjection<,> vive aqui, NO en Marten.Events.Projections

namespace Bitakora.ControlAsistencia.Projections.ControlHoras;

/// <summary>
/// Clase de proyeccion companion de AsistenciaDiaria (issue #426, receta N1 de MEF-ADR-0035: un solo
/// stream "dc:{CodigoColaborador}:{yyyyMMdd}" por fila -- mismo corte que TurnoVigenteProjection).
/// Vive en el worker, el unico ensamblado que referencia Marten y el analizador
/// JasperFx.Events.SourceGenerator.
///
/// partial es OBLIGATORIO (skills/projections/modelos-marten.md): el source generator descubre
/// Create/Apply por convencion y emite el dispatcher [GeneratedEvolver]. Sin partial el build queda
/// limpio y falla en RUNTIME al registrar la proyeccion (InvalidProjectionException); el config-test
/// ConfigurarControlHoras_RegistraAsistenciaDiariaProjectionComoAsync es lo que lo detecta.
///
/// Sin ShouldDelete: la fila nunca se borra (issue #426, notas tecnicas).
///
/// Create/Apply derivan Plan (eje 1) y las cuatro banderas de anomalia ya juzgadas (eje 2) a
/// partir de la senal estructural del evento -- DepuracionDiaRecibida no expone un metodo propio
/// de clasificacion (Tell-don't-Ask no aplica aqui: no hay comportamiento de dominio que delegar),
/// asi que la derivacion vive en el helper privado ClasificarPlan de esta clase, mapping de vista
/// segun las notas tecnicas del issue #426.
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

    // "El ultimo gana" (CA-6): cada foto reemplaza Plan, NombreTurno, las cuatro banderas y
    // HorasPorConcepto. Id/CodigoColaborador/Fecha invariantes (identidad del stream). Estado no
    // cambia en este issue.
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

    // NombreTurno null = SinProgramar; NombreTurno + franjas vacias = Descanso; NombreTurno +
    // franjas >= 1 = ConJornada (issue #426, Eje 1 -- Plan).
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
