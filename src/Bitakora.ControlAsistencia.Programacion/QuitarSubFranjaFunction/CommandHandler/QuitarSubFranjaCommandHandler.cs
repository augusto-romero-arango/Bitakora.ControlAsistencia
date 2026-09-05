using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.QuitarSubFranjaFunction.CommandHandler;

// Espejo de QuitarFranjaCommandHandler (#604): este comando tampoco construye ningun VO, asi que
// no hay canal de ArgumentException que mezclar con el de las reglas de negocio (CA-ADR-0030).
public partial class QuitarSubFranjaCommandHandler : ICommandHandlerAsync<QuitarSubFranja>
{
    private readonly IEventStore _eventStore;

    public QuitarSubFranjaCommandHandler(IEventStore eventStore) => _eventStore = eventStore;

    public Task HandleAsync(QuitarSubFranja command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
