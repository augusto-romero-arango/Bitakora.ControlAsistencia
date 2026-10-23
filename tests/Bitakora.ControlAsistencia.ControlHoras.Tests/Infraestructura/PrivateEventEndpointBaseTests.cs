using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using JasperFx.Events;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class PrivateEventEndpointBaseTests
{
    private const string JsonValido = """{"nombre": "test"}""";

    private static ServiceBusReceivedMessage CrearMensaje(string json = JsonValido)
        => ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString(json));

    [Fact]
    public async Task ProcesarMensaje_AbandonaElMensajeYRegistraMessageIdYDeliveryCount_CuandoElHandlerFalla()
    {
        var router = new FakePrivateRouter(new InvalidOperationException("Error"));
        var actions = new FakeServiceBusMessageActions();
        var logger = new FakeLogger();
        var endpoint = new StubPrivateEndpoint(router, logger);
        var mensaje = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(JsonValido), messageId: "msg-priv", deliveryCount: 4);

        await endpoint.Procesar(mensaje, actions, CancellationToken.None);

        actions.MensajeAbandonado.Should().BeTrue();
        actions.MensajeEnDeadLetter.Should().BeFalse();
        actions.MensajeCompletado.Should().BeFalse();
        logger.MensajesError.Should().ContainSingle()
            .Which.Should().Contain("msg-priv").And.Contain("4");
    }

    [Fact]
    public async Task ProcesarMensaje_AbandonaElMensaje_CuandoHayConflictoDeConcurrencia()
    {
        var router = new FakePrivateRouter(
            new EventStreamUnexpectedMaxEventIdException("cd:stream", typeof(object), 2, 3));
        var actions = new FakeServiceBusMessageActions();
        var endpoint = new StubPrivateEndpoint(router, new FakeLogger());

        await endpoint.Procesar(CrearMensaje(), actions, CancellationToken.None);

        actions.MensajeAbandonado.Should().BeTrue();
        actions.MensajeEnDeadLetter.Should().BeFalse();
    }

    [Fact]
    public async Task ProcesarMensaje_EnviaADeadLetterSinInvocarElHandler_CuandoJsonEsInvalido()
    {
        var router = new FakePrivateRouter();
        var actions = new FakeServiceBusMessageActions();
        var endpoint = new StubPrivateEndpoint(router, new FakeLogger());

        await endpoint.Procesar(CrearMensaje("no-es-json"), actions, CancellationToken.None);

        actions.MensajeEnDeadLetter.Should().BeTrue();
        actions.MensajeAbandonado.Should().BeFalse();
        router.Invocaciones.Should().Be(0);
    }

    [Fact]
    public async Task ProcesarMensaje_EnviaADeadLetterSinInvocarElHandler_CuandoElCuerpoDeserializaANull()
    {
        var router = new FakePrivateRouter();
        var actions = new FakeServiceBusMessageActions();
        var endpoint = new StubPrivateEndpoint(router, new FakeLogger());

        await endpoint.Procesar(CrearMensaje("null"), actions, CancellationToken.None);

        actions.MensajeEnDeadLetter.Should().BeTrue();
        actions.MensajeAbandonado.Should().BeFalse();
        router.Invocaciones.Should().Be(0);
    }

    [Fact]
    public async Task ProcesarMensaje_SoloRegistraWarning_CuandoSePierdeElLock()
    {
        var lockLost = new ServiceBusException("Lock expirado", ServiceBusFailureReason.MessageLockLost);
        var actions = new FakeServiceBusMessageActions(excepcionAlCompletar: lockLost);
        var logger = new FakeLogger();
        var endpoint = new StubPrivateEndpoint(new FakePrivateRouter(), logger);

        await endpoint.Procesar(CrearMensaje(), actions, CancellationToken.None);

        actions.MensajeAbandonado.Should().BeFalse();
        actions.MensajeEnDeadLetter.Should().BeFalse();
        logger.WarningLogueado.Should().BeTrue();
    }
}

internal record EventoPrivadoStub(string? Nombre) : IPrivateEvent;

internal class StubPrivateEndpoint(IPrivateEventRouter router, ILogger logger)
    : PrivateEventEndpointBase<EventoPrivadoStub>(router, logger)
{
    public Task Procesar(
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions actions,
        CancellationToken ct)
        => ProcesarMensaje(message, actions, ct);
}

internal class FakePrivateRouter(Exception? excepcion = null) : IPrivateEventRouter
{
    public int Invocaciones { get; private set; }

    public Task InvokeAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : class, IPrivateEvent
    {
        Invocaciones++;
        if (excepcion is not null) throw excepcion;
        return Task.CompletedTask;
    }
}
