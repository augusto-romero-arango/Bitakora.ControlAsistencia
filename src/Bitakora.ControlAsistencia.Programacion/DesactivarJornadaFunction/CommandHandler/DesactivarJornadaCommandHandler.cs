using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.MultiTenancy;
using Comando = Bitakora.ControlAsistencia.Programacion.DesactivarJornadaFunction.DesactivarJornada;

namespace Bitakora.ControlAsistencia.Programacion.DesactivarJornadaFunction.CommandHandler;

public partial class DesactivarJornadaCommandHandler(
    IEventStore eventStore, ILectorPlantillasPorJornada lectorPlantillas, ITenantContext tenantContext)
    : ICommandHandlerAsync<Comando>
{
    public Task HandleAsync(Comando command, CancellationToken ct = default)
        => throw new NotImplementedException();
}
