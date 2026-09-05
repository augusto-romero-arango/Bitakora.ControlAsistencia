using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.QuitarFranjaFunction.CommandHandler;

// Espejo de RetirarTurnoCommandHandler/AgregarFranjaCommandHandler en el canal de errores: sin
// invariantes de VO involucradas (este comando no construye nada), asi que no hay canal de
// ArgumentException que mezclar (CA-ADR-0030).
public partial class QuitarFranjaCommandHandler : ICommandHandlerAsync<QuitarFranja>
{
    private readonly IEventStore _eventStore;

    public QuitarFranjaCommandHandler(IEventStore eventStore) => _eventStore = eventStore;

    public Task HandleAsync(QuitarFranja command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
