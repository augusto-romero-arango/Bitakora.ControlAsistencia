// HU-67: Tests de la orquestacion generica de ServiceBusEndpointBase.
// Estos tests se escriben una sola vez y cubren todos los endpoints de ServiceBus.

using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
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
        logger.WarningLogueado.Should().BeTrue();
    }

    // Error generico -> dead-letter el mensaje
    [Fact]
    public async Task DebeEnviarADeadLetter_CuandoOcurreErrorGenerico()
    {
        var router = new FakeCommandRouter(
            excepcion: new InvalidOperationException("Error inesperado"));
        var actions = new FakeServiceBusMessageActions();
        var endpoint = new StubEndpoint(router, new FakeLogger());

        await endpoint.Procesar(CrearMensaje(), actions, CancellationToken.None);

        actions.MensajeEnDeadLetter.Should().BeTrue();
        actions.MensajeCompletado.Should().BeFalse();
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

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
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
        => throw new NotImplementedException();

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

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (logLevel == LogLevel.Warning) WarningLogueado = true;
    }

    public bool IsEnabled(LogLevel logLevel) => true;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
}
