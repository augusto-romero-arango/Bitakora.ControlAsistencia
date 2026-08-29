using Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoRegistroDeMarcacionCreado.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.Sedes;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.EstamparSedeCuandoSedeDeMarcacionResuelta.EventHandler;

// Issue #463: cierre del enriquecimiento coreografiado (MEF-ADR-0046) -- Sedes resolvio
// dispositivo->sede->CC y lo publico como SedeDeMarcacionResuelta (#467); ControlHoras -- dueno de
// la marcacion -- graba ese resultado como un hecho mas de su historia (SedeDeMarcacionIdentificada,
// ControlHoras.DomainEvents). Se consume directo con IPrivateEventHandlerAsync, sin comando espejo
// (MEF-ADR-0024 decision #8).
// MEF-ADR-0009: partial class para soportar la clase Mensajes en archivo separado.
// Stub de compilacion -- fase roja del pipeline TDD: la orquestacion real (fechas-destino con
// traslape nocturno, precondicion de ControlDiario/marcacion existente que dispara el retry del
// bus, invocacion de EstamparSede y re-publicacion condicional de DiaDepurado) es responsabilidad
// del implementer.
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

        // Mismo traslape nocturno heredado que AdicionarMarcacion (constante reutilizada, no
        // duplicada): el estampado va a los MISMOS dias-destino que la marcacion.
        var fechasDestino = horaDelDia < RegistroDeMarcacionCreadoEventHandler.HoraCorteTraslapeNocturno
            ? new[] { fechaCalendario, fechaCalendario.AddDays(-1) }
            : new[] { fechaCalendario };

        foreach (var fecha in fechasDestino)
            await EstamparSedeEnControlDiarioAsync(@event, fecha, ct);
    }

    // CA-3 (problema de orden, carrera A): este handler solo ACTUALIZA, nunca crea un ControlDiario
    // (a diferencia de RegistroDeMarcacionCreadoEventHandler). Si el stream o la marcacion aun no
    // existen, lanza para que el retry del Service Bus lo resuelva segundos despues -- sin crear
    // stream vacio ni evento.
    private async Task EstamparSedeEnControlDiarioAsync(
        SedeDeMarcacionResuelta @event, DateOnly fecha, CancellationToken ct)
    {
        var streamId = ControlDiarioAggregateRoot.ComputarStreamId(@event.CodigoColaborador, fecha);

        var existe = await _eventStore.ExistsAsync<ControlDiarioAggregateRoot>(streamId, ct);
        if (!existe)
            throw new InvalidOperationException(Mensajes.ControlDiarioNoEncontrado);

        var control = (await _eventStore.GetAggregateRootAsync<ControlDiarioAggregateRoot>(streamId, ct))!;

        var marcacionExiste = control.Marcaciones.Any(m =>
            m.TimestampNormalizado == @event.TimestampNormalizado && m.DispositivoId == @event.DispositivoId);
        if (!marcacionExiste)
            throw new InvalidOperationException(Mensajes.MarcacionNoEncontrada);

        var evento = new SedeDeMarcacionIdentificada(
            streamId,
            @event.TimestampNormalizado,
            @event.DispositivoId,
            @event.CodigoSede,
            @event.NombreSede,
            @event.CentroDeCostos);

        var eventosAntes = control.UncommittedEvents.Count;
        control.EstamparSede(evento);
        var huboCambios = control.UncommittedEvents.Count > eventosAntes;

        if (huboCambios)
            await _privateEventSender.PublishAsync(control.CrearDiaDepurado());
    }
}
