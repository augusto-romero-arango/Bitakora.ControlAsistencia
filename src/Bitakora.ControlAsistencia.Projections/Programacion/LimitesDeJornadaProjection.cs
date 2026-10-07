using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using JasperFx.Events;
using Marten.Events.Aggregation;

namespace Bitakora.ControlAsistencia.Projections.Programacion;

public sealed partial class LimitesDeJornadaProjection : SingleStreamProjection<LimitesDeJornada, string>
{
    public static LimitesDeJornada Create(IEvent<JornadaCreada> e) =>
        throw new NotImplementedException();

    public static LimitesDeJornada Apply(LimitesJornadaModificados e, LimitesDeJornada vista) =>
        throw new NotImplementedException();
}
