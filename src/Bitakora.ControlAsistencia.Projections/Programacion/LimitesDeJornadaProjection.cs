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
        Proyectar(vista.Id, e.Limites) with { Activa = vista.Activa };

    public static LimitesDeJornada Apply(JornadaDesactivada e, LimitesDeJornada vista) =>
        vista with { Activa = false };

    public static LimitesDeJornada Apply(JornadaActivada e, LimitesDeJornada vista) =>
        vista with { Activa = true };

    private static LimitesDeJornada Proyectar(string id, LimitesJornada limites) =>
        new(id,
            limites.HorasSemanales.TotalMinutos(),
            limites.TopeDiario.TotalMinutos(),
            limites.MinimoDiario.TotalMinutos(),
            limites.DiasDescansoPorSemana,
            limites.ToString());
}
