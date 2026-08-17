using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction.CommandHandler;

// HU-105: Handler del comando RegistrarMarcacion
// Flujo: verificar idempotencia via ExistsAsync -> construir evento via factory -> persistir -> publicar
// CA-4: si el stream ya existe (duplicado exacto), retornar silenciosamente sin persistir ni publicar
// Issue #270 CA-4: tras StartStream, publica el contrato de bus RegistroDeMarcacionCreado empaquetado
// por el traductor del aggregate (Tell-don't-Ask) via IPrivateEventSender. MarcacionRegistrada (evento
// de dominio persistido) ya no cruza el bus.
// Issue #275 CA-4: la normalizacion (truncar segundos) y la validacion de CodigoColaborador ya no viven aqui
// -- son responsabilidad del factory MarcacionRegistrada.Crear (MEF-ADR-0012: el handler construye el
// evento antes de pasarlo al aggregate; si la construccion falla, el throw ocurre aqui, no dentro del
// aggregate -- MEF-ADR-0004 se mantiene). La firma de Iniciar(streamId, timestampCrudo, evento) no cambia.
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
            command.CodigoColaborador, command.Timestamp);

        // CA-4, CA-9: duplicado exacto -> retorno silencioso, sin persistir ni publicar
        var existe = await _eventStore.ExistsAsync<RegistroDeMarcacionAggregateRoot>(streamId, ct);
        if (existe)
            return;

        // Issue #275 CA-1/CA-2/CA-3: el factory trunca el timestamp al minuto y valida CodigoColaborador.
        // Si falla, el throw ocurre aqui (borde del handler), no dentro del aggregate.
        var evento = MarcacionRegistrada.Crear(
            command.CodigoColaborador,
            command.Timestamp,
            command.TipoMarcacion,
            command.DispositivoId);

        var registro = RegistroDeMarcacionAggregateRoot.Iniciar(streamId, command.Timestamp, evento);
        _eventStore.StartStream(registro);

        // Issue #270 CA-4: publicar el contrato de bus (no el evento de dominio) para que
        // handlers downstream reaccionen
        await _privateEventSender.PublishAsync(registro.CrearRegistroDeMarcacionCreado());
    }
}
