// HU-67: Tests de la orquestacion generica de ServiceBusEndpointBase.
// Estos tests se escriben una sola vez y cubren todos los endpoints de ServiceBus.

using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using JasperFx.Events;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class ServiceBusEndpointBaseTests
{
    private const string JsonValido = """{"nombre": "test"}""";

    private static ServiceBusReceivedMessage CrearMensaje(string json = JsonValido)
        => ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString(json));

    // Camino feliz: deserializa, despacha al command router, completa el mensaje
    [Fact]
    public async Task DebeCompletarMensaje_CuandoProcesamientoEsExitoso()
    {
        var router = new FakeCommandRouter();
        var actions = new FakeServiceBusMessageActions();
        var endpoint = new StubEndpoint(router, new FakeLogger());

        await endpoint.Procesar(CrearMensaje(), actions, CancellationToken.None);

        actions.MensajeCompletado.Should().BeTrue();
        actions.MensajeEnDeadLetter.Should().BeFalse();
    }

    // Lock perdido -> log warning, NO dead-letter (regresion issue #48)
    [Fact]
    public async Task DebeLoguearWarning_CuandoSePierdeLock()
    {
        var lockLost = new ServiceBusException(
            "Lock expirado", ServiceBusFailureReason.MessageLockLost);
        var router = new FakeCommandRouter();
        var actions = new FakeServiceBusMessageActions(excepcionAlCompletar: lockLost);
        var logger = new FakeLogger();
        var endpoint = new StubEndpoint(router, logger);

        await endpoint.Procesar(CrearMensaje(), actions, CancellationToken.None);

        actions.MensajeEnDeadLetter.Should().BeFalse("el lock ya no es valido, no se puede dead-letter");
        actions.MensajeAbandonado.Should().BeFalse("el lock ya no es valido, no se puede abandonar");
        logger.WarningLogueado.Should().BeTrue();
    }

    [Fact]
    public async Task ProcesarMensaje_AbandonaElMensaje_CuandoElHandlerFalla()
    {
        var router = new FakeCommandRouter(
            excepcion: new InvalidOperationException("Error inesperado"));
        var actions = new FakeServiceBusMessageActions();
        var endpoint = new StubEndpoint(router, new FakeLogger());

        await endpoint.Procesar(CrearMensaje(), actions, CancellationToken.None);

        actions.MensajeAbandonado.Should().BeTrue();
        actions.MensajeEnDeadLetter.Should().BeFalse();
        actions.MensajeCompletado.Should().BeFalse();
    }

    [Fact]
    public async Task ProcesarMensaje_AbandonaElMensaje_CuandoHayConflictoDeConcurrencia()
    {
        var router = new FakeCommandRouter(
            excepcion: new EventStreamUnexpectedMaxEventIdException("cd:stream", typeof(object), 2, 3));
        var actions = new FakeServiceBusMessageActions();
        var endpoint = new StubEndpoint(router, new FakeLogger());

        await endpoint.Procesar(CrearMensaje(), actions, CancellationToken.None);

        actions.MensajeAbandonado.Should().BeTrue();
        actions.MensajeEnDeadLetter.Should().BeFalse();
    }

    [Fact]
    public async Task ProcesarMensaje_RegistraMessageIdYDeliveryCount_CuandoAbandonaElMensaje()
    {
        var router = new FakeCommandRouter(excepcion: new InvalidOperationException("Error"));
        var actions = new FakeServiceBusMessageActions();
        var logger = new FakeLogger();
        var endpoint = new StubEndpoint(router, logger);
        var mensaje = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(JsonValido), messageId: "msg-1172", deliveryCount: 3);

        await endpoint.Procesar(mensaje, actions, CancellationToken.None);

        logger.MensajesError.Should().ContainSingle()
            .Which.Should().Contain("msg-1172").And.Contain("3");
    }

    [Fact]
    public async Task ProcesarMensaje_EnviaADeadLetterSinInvocarElHandler_CuandoElCuerpoDeserializaANull()
    {
        var router = new FakeCommandRouter();
        var actions = new FakeServiceBusMessageActions();
        var endpoint = new StubEndpoint(router, new FakeLogger());

        await endpoint.Procesar(CrearMensaje("null"), actions, CancellationToken.None);

        actions.MensajeEnDeadLetter.Should().BeTrue();
        actions.MensajeAbandonado.Should().BeFalse();
        router.Invocaciones.Should().Be(0);
    }

    // JSON invalido -> dead-letter (error de deserializacion)
    [Fact]
    public async Task DebeEnviarADeadLetter_CuandoJsonEsInvalido()
    {
        var router = new FakeCommandRouter();
        var actions = new FakeServiceBusMessageActions();
        var endpoint = new StubEndpoint(router, new FakeLogger());

        await endpoint.Procesar(CrearMensaje("no-es-json"), actions, CancellationToken.None);

        actions.MensajeEnDeadLetter.Should().BeTrue();
        actions.MensajeCompletado.Should().BeFalse();
    }
}

// ---- Stub concreto minimo para testear la clase base ----

internal record EventoStub(string? Nombre);

internal class StubEndpoint(ICommandRouter commandRouter, ILogger logger)
    : ServiceBusEndpointBase<EventoStub>(commandRouter, logger)
{
    public Task Procesar(
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions actions,
        CancellationToken ct)
        => ProcesarMensaje(message, actions, ct);
}
