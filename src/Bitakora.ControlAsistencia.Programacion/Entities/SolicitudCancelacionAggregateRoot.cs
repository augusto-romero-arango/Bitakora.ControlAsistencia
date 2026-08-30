using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

// Issue #498: espejo de SolicitudProgramacionAggregateRoot para la solicitud de cancelacion.
public partial class SolicitudCancelacionAggregateRoot : AggregateRoot
{
    internal ColaboradorProgramado? Colaborador { get; private set; }
    internal IReadOnlyList<DateOnly> Fechas { get; private set; } = [];

    public void Apply(CancelacionProgramacionSolicitada e)
    {
        Id = e.Id.ToString();
        Colaborador = e.Colaborador;
        Fechas = e.Fechas;
    }

    internal static SolicitudCancelacionAggregateRoot Iniciar(CancelacionProgramacionSolicitada evento)
    {
        var solicitud = new SolicitudCancelacionAggregateRoot();
        solicitud._uncommittedEvents.Add(evento);
        solicitud.Apply(evento);
        return solicitud;
    }
}
