using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

// Issue #498: espejo de SolicitudProgramacionAggregateRoot para la solicitud de cancelacion.
public partial class SolicitudCancelacionAggregateRoot : AggregateRoot
{
    internal ColaboradorProgramado? Colaborador { get; private set; }
    internal IReadOnlyList<DateOnly> Fechas { get; private set; } = [];

    public void Apply(CancelacionProgramacionSolicitada e) => throw new NotImplementedException();
}
