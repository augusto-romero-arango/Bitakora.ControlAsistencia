using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.IniciarVinculacionFunction.CommandHandler;

// Issue #378: handler del comando IniciarVinculacion -- absorbe y reemplaza a
// ReingresarColaboradorCommandHandler (issue #350). El aggregate declina con resultado
// (CA-ADR-0030): nunca lanza ni emite un evento de fallo persistido, y este handler traduce la
// razon del rechazo a la excepcion que el borde convierte en status code (MEF-ADR-0004 capa 2):
// stream inexistente -> 404, regla de negocio violada -> 409. En el camino de exito el aggregate
// deja VinculacionIniciada en _uncommittedEvents -- el middleware persiste via SaveChanges. Sin
// publicacion a bus (event-sourcing puro, "Consumidores: ninguno").
// STUB de la fase roja del pipeline TDD (test-writer): la logica real (parseo tipado,
// rehidratacion, invocacion de ColaboradorAggregateRoot.IniciarVinculacion, traduccion del
// resultado a InvalidOperationException/KeyNotFoundException) la escribe el implementer en la fase
// verde -- este agente nunca escribe implementacion real.
public partial class IniciarVinculacionCommandHandler : ICommandHandlerAsync<IniciarVinculacion>
{
    private readonly IEventStore _eventStore;

    public IniciarVinculacionCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public Task HandleAsync(IniciarVinculacion command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
