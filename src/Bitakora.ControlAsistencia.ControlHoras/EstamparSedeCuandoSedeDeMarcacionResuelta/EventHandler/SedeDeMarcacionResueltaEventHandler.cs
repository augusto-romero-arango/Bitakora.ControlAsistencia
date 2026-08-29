using Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoRegistroDeMarcacionCreado.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.Sedes;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.EstamparSedeCuandoSedeDeMarcacionResuelta.EventHandler;

// Paso 4 del enriquecimiento coreografiado (MEF-ADR-0046): ControlHoras -- dueno de la marcacion --
// graba como hecho propio la sede que Sedes resolvio. El evento del bus es espejo directo del hecho
// a registrar, asi que se consume sin comando intermedio (MEF-ADR-0024 decision #8).
// partial: la clase Mensajes vive en archivo separado (MEF-ADR-0009).
public partial class SedeDeMarcacionResueltaEventHandler
    : IPrivateEventHandlerAsync<SedeDeMarcacionResuelta>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public SedeDeMarcacionResueltaEventHandler(
        IEventStore eventStore,
        IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public async Task HandleAsync(SedeDeMarcacionResuelta @event, CancellationToken ct = default)
    {
        var fechaCalendario = DateOnly.FromDateTime(@event.TimestampNormalizado);
        var horaDelDia = TimeOnly.FromDateTime(@event.TimestampNormalizado);

        // El estampado debe ir a los MISMOS dias-destino que la marcacion: de ahi que la ventana
        // nocturna se lea de la constante del handler vecino en vez de duplicarla aqui.
        var fechasDestino = horaDelDia < RegistroDeMarcacionCreadoEventHandler.HoraCorteTraslapeNocturno
            ? new[] { fechaCalendario, fechaCalendario.AddDays(-1) }
            : new[] { fechaCalendario };

        foreach (var fecha in fechasDestino)
            await EstamparSedeEnControlDiarioAsync(@event, fecha, ct);
    }

    // CA-3 (problema de orden, carrera A): este handler solo ACTUALIZA, nunca crea un ControlDiario
    // (a diferencia de RegistroDeMarcacionCreadoEventHandler). Si el stream o la marcacion aun no
    // existen, lanza para que el retry del Service Bus lo resuelva segundos despues -- sin crear
    // stream vacio ni evento. La segunda precondicion la declina el aggregate (CA-ADR-0030): el
    // handler traduce el resultado, no inspecciona Marcaciones.
    private async Task EstamparSedeEnControlDiarioAsync(
        SedeDeMarcacionResuelta @event, DateOnly fecha, CancellationToken ct)
    {
        var streamId = ControlDiarioAggregateRoot.ComputarStreamId(@event.CodigoColaborador, fecha);

        var existe = await _eventStore.ExistsAsync<ControlDiarioAggregateRoot>(streamId, ct);
        if (!existe)
            throw new InvalidOperationException(Mensajes.ControlDiarioNoEncontrado);

        var control = (await _eventStore.GetAggregateRootAsync<ControlDiarioAggregateRoot>(streamId, ct))!;

        var resultado = control.EstamparSede(new SedeDeMarcacionIdentificada(
            streamId,
            @event.TimestampNormalizado,
            @event.DispositivoId,
            @event.CodigoSede,
            @event.NombreSede,
            @event.CentroDeCostos));

        if (resultado == ResultadoEstampadoSede.MarcacionNoEncontrada)
            throw new InvalidOperationException(Mensajes.MarcacionNoEncontrada);

        if (resultado == ResultadoEstampadoSede.Estampada)
            await _privateEventSender.PublishAsync(control.CrearDiaDepurado());
    }
}
