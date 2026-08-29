using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Marten.Events.Aggregation; // SingleStreamProjection<,> vive aqui, NO en Marten.Events.Projections
// Alias, no nombre corto: ReadModels.ControlHoras declara homonimos FranjaDepurada/MarcacionDelDia
// (espejo por rol del mismo termino, MEF-ADR-0039 decision 6) que colisionan (CS0104) con los de
// DomainEvents que evento.Franjas/evento.Marcaciones tipan aqui.
using EventoFranjaDepurada = Bitakora.ControlAsistencia.ControlHoras.DomainEvents.FranjaDepurada;
using EventoMarcacionDelDia = Bitakora.ControlAsistencia.ControlHoras.DomainEvents.MarcacionDelDia;

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
            ConflictoDeSedePendiente: EsConflictoDeSedePendiente(evento.Franjas, evento.Marcaciones),
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
            ConflictoDeSedePendiente = EsConflictoDeSedePendiente(evento.Franjas, evento.Marcaciones),
            HorasPorConcepto = evento.HorasDiscriminadas.HorasPorConcepto
        };
    }

    // Issue #492 CA-2: aval del vacio -- un stream puede NACER con DiaAprobado (dia sin datos,
    // #489 CA-7). Sin franjas ni marcaciones que clasificar, el plan queda SinProgramar y ninguna
    // bandera se enciende: el dia avalado como "no vino y no debia venir" aparece aprobado y limpio.
    public static AsistenciaDiaria Create(DiaAprobado evento) =>
        new(
            evento.Id,
            evento.CodigoColaborador,
            evento.Fecha,
            EstadoAsistencia.Aprobado,
            PlanDelDia.SinProgramar,
            NombreTurno: null,
            NoSePresento: false,
            FranjasIncompletas: false,
            VinoEnDescanso: false,
            TrabajoSinProgramacion: false,
            ConflictoDeSedePendiente: false,
            HorasPorConcepto: new Dictionary<string, decimal>());

    // Issue #492 CA-1/CA-3: cierre del ciclo Provisional -> Aprobado. Estado pasa a Aprobado y
    // ConflictoDeSedePendiente se apaga (las decisiones de sede se tomaron en el acto de aprobar);
    // el resto de la fila (Plan, banderas de anomalia ya juzgadas, NombreTurno, HorasPorConcepto)
    // queda intacto -- la aprobacion no reescribe la historia, la pone en firme.
    public static AsistenciaDiaria Apply(DiaAprobado evento, AsistenciaDiaria vista) =>
        vista with
        {
            Estado = EstadoAsistencia.Aprobado,
            ConflictoDeSedePendiente = false
        };

    // DepuracionPosAprobacionRecibida (#491) no se consume aqui: sin un metodo Create/Apply que lo
    // tipe, el source generator no lo dispatchea y el daemon simplemente lo salta -- decision
    // deliberada del issue #492 (NO depende de #491), no un olvido. El dia aprobado no se mueve en
    // la vista cuando llega ese evento.

    private static PlanDelDia ClasificarPlan(string? nombreTurno, IReadOnlyList<EventoFranjaDepurada> franjas) =>
        nombreTurno switch
        {
            null => PlanDelDia.SinProgramar,
            _ when franjas.Count == 0 => PlanDelDia.Descanso,
            _ => PlanDelDia.ConJornada
        };

    private static bool EsNoSePresento(PlanDelDia plan, IReadOnlyList<EventoMarcacionDelDia> marcaciones) =>
        plan == PlanDelDia.ConJornada && marcaciones.Count == 0;

    private static bool EsFranjasIncompletas(PlanDelDia plan, IReadOnlyList<EventoFranjaDepurada> franjas) =>
        plan == PlanDelDia.ConJornada && franjas.Any(franja => franja.EsAnomala);

    private static bool EsVinoEnDescanso(PlanDelDia plan, IReadOnlyList<EventoMarcacionDelDia> marcaciones) =>
        plan == PlanDelDia.Descanso && marcaciones.Count > 0;

    private static bool EsTrabajoSinProgramacion(PlanDelDia plan, IReadOnlyList<EventoMarcacionDelDia> marcaciones) =>
        plan == PlanDelDia.SinProgramar && marcaciones.Count > 0;

    // Re-derivacion de la politica de DiaCalculadoAggregateRoot.DerivarSedeDeFranja, duplicada a
    // proposito (MEF-ADR-0018): si cambia alla, cambia aqui. Solo el booleano -- sede efectiva, CC y
    // candidatas con nombre quedan fuera: son superficie de investigacion y ya las sirve el aggregate.
    private static bool EsConflictoDeSedePendiente(
        IReadOnlyList<EventoFranjaDepurada> franjas, IReadOnlyList<EventoMarcacionDelDia> marcaciones) =>
        franjas.Any(franja => CandidatasDeSede(franja, marcaciones).Distinct().Count() >= 2);

    private static IEnumerable<string> CandidatasDeSede(
        EventoFranjaDepurada franja, IReadOnlyList<EventoMarcacionDelDia> marcaciones)
    {
        if (franja.CodigoSedeProgramada is not null)
            yield return franja.CodigoSedeProgramada;

        foreach (var marcacion in marcaciones.Where(marcacion => PerteneceA(marcacion, franja)))
            if (marcacion.CodigoSede is not null)
                yield return marcacion.CodigoSede;
    }

    // Misma regla de pertenencia que DiaCalculadoAggregateRoot.PerteneceA (MEF-ADR-0018).
    private static bool PerteneceA(EventoMarcacionDelDia marcacion, EventoFranjaDepurada franja) =>
        marcacion.Timestamp == franja.Entrada || marcacion.Timestamp == franja.Salida;
}
