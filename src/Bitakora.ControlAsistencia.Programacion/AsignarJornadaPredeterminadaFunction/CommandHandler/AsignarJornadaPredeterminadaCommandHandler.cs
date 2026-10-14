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

        var jornada = await eventStore.GetAggregateRootAsync<Jornada>(command.JornadaId.ToString(), ct);
        if (jornada is null)
            throw new RecursoNoEncontradoException(Mensajes.JornadaNoEncontrada);

        var streamId = PreferenciasProgramacion.ComputarStreamId(tenantContext.TenantId);
        var preferencias = await eventStore.GetAggregateRootAsync<PreferenciasProgramacion>(streamId, ct);
        if (preferencias!.JornadaPredeterminada() != command.JornadaId && !jornada.Activa)
            throw new ReglaDeNegocioDeclinadaException(Mensajes.JornadaDesactivada);

        preferencias.AsignarJornadaPredeterminada(command.JornadaId, tenantContext.TenantId);
    }
}
