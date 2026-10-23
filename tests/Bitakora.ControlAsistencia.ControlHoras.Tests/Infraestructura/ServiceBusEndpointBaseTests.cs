// HU-67: Tests de la orquestacion generica de ServiceBusEndpointBase.
// Estos tests se escriben una sola vez y cubren todos los endpoints de ServiceBus.

using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;
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
    public async Task DebeAbandonarMensaje_CuandoOcurreErrorGenerico()
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
    public async Task DebeAbandonarMensaje_CuandoHayConflictoDeConcurrencia()
    {
        var router = new FakeCommandRouter(
            excepcion: new InvalidOperationException("23505 pk_mt_events_stream_and_version: conflicto de concurrencia"));
        var actions = new FakeServiceBusMessageActions();
        var endpoint = new StubEndpoint(router, new FakeLogger());

        await endpoint.Procesar(CrearMensaje(), actions, CancellationToken.None);

        actions.MensajeAbandonado.Should().BeTrue();
        actions.MensajeEnDeadLetter.Should().BeFalse();
    }

    [Fact]
    public async Task DebeLoguearMessageIdYDeliveryCount_CuandoAbandonaMensaje()
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
    public async Task DebeEnviarADeadLetterSinInvocarHandler_CuandoElCuerpoDeserializaANull()
    {
        var router = new FakeCommandRouter();
        var actions = new FakeServiceBusMessageActions();
        var endpoint = new StubEndpoint(router, new FakeLogger());

        await endpoint.Procesar(CrearMensaje("null"), actions, CancellationToken.None);

        actions.MensajeEnDeadLetter.Should().BeTrue();
        actions.MensajeAbandonado.Should().BeFalse();
        router.Invocaciones.Should().Be(0);
    }

    [Fact]
    public async Task PrivateEvent_DebeAbandonarMensaje_CuandoElHandlerFalla()
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
        logger.MensajesError.Should().ContainSingle()
            .Which.Should().Contain("msg-priv").And.Contain("4");
    }

    [Fact]
    public async Task PrivateEvent_DebeEnviarADeadLetterSinInvocarHandler_CuandoJsonEsInvalido()
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
    public async Task PrivateEvent_DebeEnviarADeadLetter_CuandoElCuerpoDeserializaANull()
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
    public async Task PrivateEvent_SoloLoguea_CuandoSePierdeLock()
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

// ---- Fakes manuales - NO NSubstitute ----

internal class FakeCommandRouter : ICommandRouter
{
    private readonly Exception? _excepcion;

    public FakeCommandRouter(Exception? excepcion = null) => _excepcion = excepcion;
    public int Invocaciones { get; private set; }

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        Invocaciones++;
        if (_excepcion is not null) throw _excepcion;
        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}

internal class FakeServiceBusMessageActions : ServiceBusMessageActions
{
    private readonly Exception? _excepcionAlCompletar;

    public bool MensajeCompletado { get; private set; }
    public bool MensajeEnDeadLetter { get; private set; }
    public bool MensajeAbandonado { get; private set; }

    public FakeServiceBusMessageActions(Exception? excepcionAlCompletar = null)
        => _excepcionAlCompletar = excepcionAlCompletar;

    public override Task CompleteMessageAsync(
        ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
    {
        if (_excepcionAlCompletar is not null) throw _excepcionAlCompletar;
        MensajeCompletado = true;
        return Task.CompletedTask;
    }

    public override Task DeadLetterMessageAsync(
        ServiceBusReceivedMessage message,
        Dictionary<string, object>? propertiesToModify = null,
        string? deadLetterReason = null,
        string? deadLetterErrorDescription = null,
        CancellationToken cancellationToken = default)
    {
        MensajeEnDeadLetter = true;
        return Task.CompletedTask;
    }

    public override Task AbandonMessageAsync(
        ServiceBusReceivedMessage message,
        IDictionary<string, object>? propertiesToModify = null,
        CancellationToken cancellationToken = default)
    {
        MensajeAbandonado = true;
        return Task.CompletedTask;
    }

    public override Task DeferMessageAsync(
        ServiceBusReceivedMessage message,
        IDictionary<string, object>? propertiesToModify = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public override Task RenewMessageLockAsync(
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}

internal class FakeLogger : ILogger
{
    public bool WarningLogueado { get; private set; }
    public List<string> MensajesError { get; } = [];

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (logLevel == LogLevel.Warning) WarningLogueado = true;
        if (logLevel == LogLevel.Error) MensajesError.Add(formatter(state, exception));
    }

    public bool IsEnabled(LogLevel logLevel) => true;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
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
