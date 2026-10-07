using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

public partial class PreferenciasProgramacion : AggregateRoot
{
    private Guid _jornadaPredeterminadaId;

    internal static string ComputarStreamId(string tenantId) => throw new NotImplementedException();

    internal static PreferenciasProgramacion Iniciar(JornadaPredeterminadaAsignada evento) =>
        throw new NotImplementedException();

    internal Guid JornadaPredeterminada() => throw new NotImplementedException();

    public void Apply(JornadaPredeterminadaAsignada evento) => throw new NotImplementedException();
}
