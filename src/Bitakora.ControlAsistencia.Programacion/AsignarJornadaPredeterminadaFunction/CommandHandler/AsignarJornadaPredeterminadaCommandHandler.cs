using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.MultiTenancy;
using Comando = Bitakora.ControlAsistencia.Programacion.AsignarJornadaPredeterminadaFunction.AsignarJornadaPredeterminada;

namespace Bitakora.ControlAsistencia.Programacion.AsignarJornadaPredeterminadaFunction.CommandHandler;

public partial class AsignarJornadaPredeterminadaCommandHandler(
    IEventStore eventStore, IAseguradorJornadaPredeterminada asegurador, ITenantContext tenantContext)
    : ICommandHandlerAsync<Comando>
{
    public Task HandleAsync(Comando command, CancellationToken ct = default)
        => throw new NotImplementedException();
}
