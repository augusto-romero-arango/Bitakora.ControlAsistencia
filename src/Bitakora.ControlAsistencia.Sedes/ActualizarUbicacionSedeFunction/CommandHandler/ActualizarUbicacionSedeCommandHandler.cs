using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Sedes.ActualizarUbicacionSedeFunction.CommandHandler;

// Issue #457: handler del comando ActualizarUbicacionSede (precedente CorregirNombresCommandHandler).
// CA-ADR-0030 / MEF-ADR-0004 capa 2: sede inexistente -> 404 via KeyNotFoundException con mensaje
// .resx. Sin caso 409: este comando no tiene reglas de estado -- la bandera Activa (issue #459) no
// se interroga aqui (CA-5, sede desactivada sigue editable). Sin publicacion a bus (Consumidores:
// ninguno en este issue). Fase roja: stub minimo, el implementer completa.
public partial class ActualizarUbicacionSedeCommandHandler : ICommandHandlerAsync<ActualizarUbicacionSede>
{
    private readonly IEventStore _eventStore;

    public ActualizarUbicacionSedeCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public Task HandleAsync(ActualizarUbicacionSede command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
