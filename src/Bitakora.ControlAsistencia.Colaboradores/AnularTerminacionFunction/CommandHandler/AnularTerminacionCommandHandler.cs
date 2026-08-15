using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction.CommandHandler;

// Issue #354: handler del comando AnularTerminacion. Usa el mecanismo "declinar con resultado"
// (CA-ADR-0030) con una unica razon de rechazo -- la ultima vinculacion no tiene terminacion
// registrada --, que este handler traduce a InvalidOperationException/409 con mensaje .resx. El 404
// es precondicion de orquestacion (MEF-ADR-0004 capa 2), no regla del aggregate. En el camino de
// exito el aggregate ya dejo TerminacionAnulada en _uncommittedEvents -- el middleware persiste via
// SaveChanges. Sin publicacion a bus (event-sourcing puro, issue #354 "Consumidores: ninguno").
// Issue #379 (MEF-ADR-0043 paso 4, CA-5): el comando gana el campo Codigo -- el handler debe
// pasarlo a ColaboradorAggregateRoot.AnularTerminacion(codigo) y traducir el nuevo caso
// ResultadoAnulacionTerminacion.CodigoNoCorresponde a
// InvalidOperationException(Mensajes.CodigoNoCorresponde) (-> 409), evaluada ANTES que
// VinculacionAbierta.
// STUB (fase roja, issue #379): el cuerpo completo queda para el implementer -- este agente nunca
// escribe implementacion real.
public partial class AnularTerminacionCommandHandler : ICommandHandlerAsync<AnularTerminacion>
{
    private readonly IEventStore _eventStore;

    public AnularTerminacionCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public Task HandleAsync(AnularTerminacion command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
