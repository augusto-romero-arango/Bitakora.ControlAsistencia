using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.CancelarAusenciaFunction.CommandHandler;

public partial class CancelarAusenciaCommandHandler : ICommandHandlerAsync<CancelarAusencia>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public CancelarAusenciaCommandHandler(IEventStore eventStore, IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public async Task HandleAsync(CancelarAusencia command, CancellationToken ct = default)
    {
        var ausencias = await _eventStore.GetAggregateRootAsync<AusenciasColaborador>(
            AusenciasColaborador.ComputarStreamId(command.CodigoColaborador), ct);
        if (ausencias is null)
            throw new RecursoNoEncontradoException(Mensajes.AusenciaNoEncontrada);

        switch (ausencias.CancelarFechas(command.AusenciaId, command.Fechas))
        {
            case ResultadoCancelarAusencia.AusenciaInexistente:
                throw new RecursoNoEncontradoException(Mensajes.AusenciaNoEncontrada);
            case ResultadoCancelarAusencia.Canceladas canceladas:
                var resumen = new ResumenColaborador(
                    canceladas.Colaborador.Identificacion,
                    canceladas.Colaborador.CodigoColaborador,
                    canceladas.Colaborador.NombreCompleto);
                IPrivateEvent[] eventos = [.. canceladas.Fechas
                    .Select(f => new AusenciaDiariaCancelada(command.AusenciaId, resumen, f))];
                await _privateEventSender.PublishAsync(eventos);
                break;
        }
    }
}
