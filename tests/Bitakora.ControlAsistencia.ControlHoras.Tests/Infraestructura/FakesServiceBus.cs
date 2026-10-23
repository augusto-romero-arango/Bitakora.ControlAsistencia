using Azure.Messaging.ServiceBus;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

internal class FakeCommandRouter : ICommandRouter
{
    private readonly Exception? _excepcion;

    public FakeCommandRouter(Exception? excepcion = null) => _excepcion = excepcion;
    public int Invocaciones { get; private set; }
    public List<object> Comandos { get; } = [];

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        Invocaciones++;
        Comandos.Add(command);
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
    public List<string> MensajesWarning { get; } = [];

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (logLevel == LogLevel.Warning)
        {
            WarningLogueado = true;
            MensajesWarning.Add(formatter(state, exception));
        }
        if (logLevel == LogLevel.Error) MensajesError.Add(formatter(state, exception));
    }

    public bool IsEnabled(LogLevel logLevel) => true;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
}
