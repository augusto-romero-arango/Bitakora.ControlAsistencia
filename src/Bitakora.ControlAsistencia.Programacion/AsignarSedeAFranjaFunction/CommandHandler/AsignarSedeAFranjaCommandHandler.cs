using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.AsignarSedeAFranjaFunction.CommandHandler;

// Mismo mecanismo "declinar con resultado" (CA-ADR-0030) que QuitarFranjaCommandHandler: el
// aggregate resuelve ConSede/TieneSedePrearmada; la ArgumentException de sede incompleta la deja
// subir FranjaOrdinaria.Crear (via ConSede), no este handler.
public partial class AsignarSedeAFranjaCommandHandler : ICommandHandlerAsync<AsignarSedeAFranja>
{
    private readonly IEventStore _eventStore;

    public AsignarSedeAFranjaCommandHandler(IEventStore eventStore) => _eventStore = eventStore;

    public Task HandleAsync(AsignarSedeAFranja command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
