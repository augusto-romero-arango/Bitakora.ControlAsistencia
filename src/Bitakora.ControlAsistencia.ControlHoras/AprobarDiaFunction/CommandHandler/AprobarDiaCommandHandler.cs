using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.AprobarDiaFunction.CommandHandler;

// Issue #489: handler del acto de aprobar. CA-ADR-0030: el aggregate declina con resultado (nunca
// lanza, nunca emite evento de fallo persistido); este handler traduce la razon del rechazo a
// InvalidOperationException (-> 409, MEF-ADR-0004 capa 2). Aval del vacio (CA-7): un stream
// inexistente tambien es un acto valido -- crea el stream con DiaAprobado como primer evento.
// partial: la clase Mensajes vive en archivo separado (MEF-ADR-0009).
public partial class AprobarDiaCommandHandler : ICommandHandlerAsync<AprobarDia>
{
    private readonly IEventStore _eventStore;

    public AprobarDiaCommandHandler(IEventStore eventStore) => _eventStore = eventStore;

    public Task HandleAsync(AprobarDia command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
