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
        Proyectar(vista.Id, e.Limites);

    private static LimitesDeJornada Proyectar(string id, LimitesJornada limites) =>
        new(id,
            MinutosTotales(limites.HorasSemanales),
            MinutosTotales(limites.TopeDiario),
            MinutosTotales(limites.MinimoDiario),
            limites.DiasDescansoPorSemana,
            limites.ToString());

    private static int MinutosTotales(HorasYMinutos tiempo) => tiempo.Horas * 60 + tiempo.Minutos;
}
