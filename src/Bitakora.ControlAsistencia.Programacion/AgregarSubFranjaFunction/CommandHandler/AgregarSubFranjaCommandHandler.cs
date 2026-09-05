using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction.CommandHandler;

// Mismo criterio de canales que AgregarFranjaCommandHandler (#602, CA-ADR-0030): la
// ArgumentException de FranjaOrdinaria.ConDescanso/ConExtra sube sin capturarse -- solo el
// resultado del aggregate (TurnoRetirado/TurnoEsDescanso/FranjaNoExiste) se traduce aqui.
public partial class AgregarSubFranjaCommandHandler : ICommandHandlerAsync<AgregarSubFranja>
{
    private readonly IEventStore _eventStore;

    public AgregarSubFranjaCommandHandler(IEventStore eventStore) => _eventStore = eventStore;

    public Task HandleAsync(AgregarSubFranja command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
