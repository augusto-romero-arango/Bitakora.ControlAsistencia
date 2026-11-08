using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using JasperFx.Events;
using Marten.Events.Aggregation;

namespace Bitakora.ControlAsistencia.Projections.Programacion;

/// <summary>
/// Clase de proyeccion companion de LimitesDeJornada (receta N1: un documento por stream de
/// Jornada; MEF-ADR-0035). partial es obligatorio para el source generator de Marten.
/// </summary>
public sealed partial class LimitesDeJornadaProjection : SingleStreamProjection<LimitesDeJornada, string>
{
    public static LimitesDeJornada Create(IEvent<JornadaCreada> e) =>
        Proyectar(e.StreamKey!, e.Data.Limites);

    public static LimitesDeJornada Apply(LimitesJornadaModificados e, LimitesDeJornada vista) =>
        Proyectar(vista.Id, e.Limites, ModalidadDe(vista), vista.DescansoAcumulado) with { Activa = vista.Activa };

    public static LimitesDeJornada Apply(JornadaDesactivada e, LimitesDeJornada vista) =>
        vista with { Activa = false };

    public static LimitesDeJornada Apply(JornadaActivada e, LimitesDeJornada vista) =>
        vista with { Activa = true };

    public static LimitesDeJornada Apply(ModalidadDeJornadaAsignada e, LimitesDeJornada vista) =>
        Recomponer(vista, e.Modalidad, vista.DescansoAcumulado);

    public static LimitesDeJornada Apply(ModalidadDeJornadaQuitada e, LimitesDeJornada vista) =>
        Recomponer(vista, null, vista.DescansoAcumulado);

    public static LimitesDeJornada Apply(DescansoAcumuladoDeJornadaMarcado e, LimitesDeJornada vista) =>
        Recomponer(vista, ModalidadDe(vista), true);

    public static LimitesDeJornada Apply(DescansoAcumuladoDeJornadaDesmarcado e, LimitesDeJornada vista) =>
        Recomponer(vista, ModalidadDe(vista), false);

    private static LimitesDeJornada Recomponer(LimitesDeJornada vista, ModalidadJornada? modalidad, bool descansoAcumulado) =>
        vista with
        {
            Modalidad = modalidad?.Nombre,
            DescansoAcumulado = descansoAcumulado,
            Descripcion = DescripcionJornada.Componer(LimitesDe(vista), modalidad, descansoAcumulado),
        };

    private static ModalidadJornada? ModalidadDe(LimitesDeJornada vista) =>
        vista.Modalidad is null ? null : ModalidadJornada.Desde(vista.Modalidad);

    private static LimitesJornada LimitesDe(LimitesDeJornada vista) =>
        LimitesJornada.Crear(
            DeMinutos(vista.HorasSemanalesEnMinutos),
            DeMinutos(vista.TopeDiarioEnMinutos),
            DeMinutos(vista.MinimoDiarioEnMinutos),
            vista.DiasDescansoPorSemana);

    private static HorasYMinutos DeMinutos(int minutos) => HorasYMinutos.Crear(minutos / 60, minutos % 60);

    private static LimitesDeJornada Proyectar(string id, LimitesJornada limites, ModalidadJornada? modalidad = null, bool descansoAcumulado = false) =>
        new(id,
            limites.HorasSemanales.TotalMinutos(),
            limites.TopeDiario.TotalMinutos(),
            limites.MinimoDiario.TotalMinutos(),
            limites.DiasDescansoPorSemana,
            DescripcionJornada.Componer(limites, modalidad, descansoAcumulado),
            Modalidad: modalidad?.Nombre,
            DescansoAcumulado: descansoAcumulado);
}
