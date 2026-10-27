using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.MultiTenancy;
using Comando = Bitakora.ControlAsistencia.Programacion.DesactivarJornadaFunction.DesactivarJornada;

namespace Bitakora.ControlAsistencia.Programacion.DesactivarJornadaFunction.CommandHandler;

public partial class DesactivarJornadaCommandHandler(
    IEventStore eventStore, ILectorPlantillasPorJornada lectorPlantillas,
    ILectorPlantillasTurnosPorJornada lectorPlantillasTurnos, ITenantContext tenantContext)
    : ICommandHandlerAsync<Comando>
{
    public async Task HandleAsync(Comando command, CancellationToken ct = default)
    {
        var jornada = await eventStore.GetAggregateRootAsync<Jornada>(command.JornadaId.ToString(), ct);
        if (jornada is null)
            throw new RecursoNoEncontradoException(Mensajes.JornadaNoEncontrada);

        if (!jornada.Activa)
            return;

        var preferencias = await eventStore.GetAggregateRootAsync<PreferenciasProgramacion>(
            PreferenciasProgramacion.ComputarStreamId(tenantContext.TenantId), ct);
        if (preferencias?.JornadaPredeterminada() == command.JornadaId)
            throw new ReglaDeNegocioDeclinadaException(Mensajes.JornadaPredeterminada);

        var plantillas = (await lectorPlantillas.ObtenerPlantillaIdsAsync(command.JornadaId, ct))
            .Concat(await lectorPlantillasTurnos.ObtenerPlantillaIdsAsync(command.JornadaId, ct))
            .ToList();
        if (plantillas.Count > 0)
            throw new ReglaDeNegocioDeclinadaException(
                $"{Mensajes.JornadaEnUsoPorPlantillas} {string.Join(", ", plantillas)}");

        jornada.Desactivar();
    }
}
