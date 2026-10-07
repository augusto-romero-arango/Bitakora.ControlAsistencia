using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.MultiTenancy;
using Comando = Bitakora.ControlAsistencia.Programacion.AsignarJornadaPredeterminadaFunction.AsignarJornadaPredeterminada;

namespace Bitakora.ControlAsistencia.Programacion.AsignarJornadaPredeterminadaFunction.CommandHandler;

public partial class AsignarJornadaPredeterminadaCommandHandler(
    IEventStore eventStore, IAseguradorJornadaPredeterminada asegurador, ITenantContext tenantContext)
    : ICommandHandlerAsync<Comando>
{
    public async Task HandleAsync(Comando command, CancellationToken ct = default)
    {
        await asegurador.AsegurarAsync(ct);

        var existe = await eventStore.ExistsAsync<Jornada>(command.JornadaId.ToString(), ct);
        if (!existe)
            throw new RecursoNoEncontradoException(Mensajes.JornadaNoEncontrada);

        var streamId = PreferenciasProgramacion.ComputarStreamId(tenantContext.TenantId);
        var preferencias = await eventStore.GetAggregateRootAsync<PreferenciasProgramacion>(streamId, ct);
        preferencias!.AsignarJornadaPredeterminada(command.JornadaId, tenantContext.TenantId);
    }
}
