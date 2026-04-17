using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction.Eventos;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction.CommandHandler;

// HU-105: Handler del comando RegistrarMarcacion
// Flujo: verificar idempotencia via ExistsAsync -> normalizar timestamp -> persistir -> publicar
// CA-4: si el stream ya existe (duplicado exacto), retornar silenciosamente sin persistir ni publicar
// CA-8: tras persistir exitosamente, publicar MarcacionRegistrada via IPrivateEventSender
// ADR-0015: partial class para soportar clase Mensajes en archivo separado si se requiere
public partial class RegistrarMarcacionCommandHandler : ICommandHandlerAsync<RegistrarMarcacion>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public RegistrarMarcacionCommandHandler(IEventStore eventStore, IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public async Task HandleAsync(RegistrarMarcacion command, CancellationToken ct = default)
    {
        var streamId = RegistroDeMarcacionAggregateRoot.ComputarStreamId(
            command.EmpleadoId, command.Timestamp);

        // CA-4, CA-9: duplicado exacto -> retorno silencioso, sin persistir ni publicar
        var existe = await _eventStore.ExistsAsync<RegistroDeMarcacionAggregateRoot>(streamId, ct);
        if (existe)
            return;

        // CA-2: truncar segundos al minuto (floor) antes de emitir el evento
        var timestampNormalizado = TruncarAlMinuto(command.Timestamp);

        var evento = new MarcacionRegistrada(
            command.EmpleadoId,
            timestampNormalizado,
            command.TipoMarcacion,
            command.DispositivoId);

        var registro = RegistroDeMarcacionAggregateRoot.Iniciar(streamId, command.Timestamp, evento);
        _eventStore.StartStream(registro);

        // CA-8: publicar el evento para que handlers downstream reaccionen
        await _privateEventSender.PublishAsync(evento);
    }

    private static DateTime TruncarAlMinuto(DateTime timestamp) =>
        new(timestamp.Year, timestamp.Month, timestamp.Day,
            timestamp.Hour, timestamp.Minute, 0, timestamp.Kind);
}
