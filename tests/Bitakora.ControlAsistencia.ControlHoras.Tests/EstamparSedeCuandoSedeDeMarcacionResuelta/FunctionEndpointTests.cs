using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.ControlHoras.EstamparSedeCuandoSedeDeMarcacionResuelta;
using Cosmos.EventDriven.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.EstamparSedeCuandoSedeDeMarcacionResuelta;

public class FunctionEndpointTests
{
    // JSON en formato camelCase - Wolverine serializa con camelCase por defecto al publicar al
    // Service Bus (mismo formato que Sedes usa para publicar SedeDeMarcacionResuelta, #467).
    private const string JsonFormatoWolverine = """
        {
          "codigoColaborador": "EMP-001",
          "timestampNormalizado": "2026-03-15T08:09:00",
          "dispositivoId": "DEV-001",
          "codigoSede": "001",
          "nombreSede": "Sede Principal",
          "centroDeCostos": "CC-100"
        }
        """;

    private static ServiceBusReceivedMessage CrearMensaje()
        => ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString(JsonFormatoWolverine));

    [Fact]
    public async Task EstamparSedeCuandoSedeDeMarcacionResuelta_CompletaMensaje_CuandoProcesamientoEsExitoso()
    {
        var router = new FakePrivateEventRouter();
        var messageActions = new FakeServiceBusMessageActions();
        var logger = new FakeLogger();
        var endpoint = new FunctionEndpoint(router, logger);

        await endpoint.Run(CrearMensaje(), messageActions, CancellationToken.None);

        messageActions.MensajeCompletado.Should().BeTrue();
        messageActions.MensajeEnDeadLetter.Should().BeFalse();
    }

    // Regresion #48: con el lock perdido no se puede dead-letter -- solo warning; Service Bus
    // re-entrega el mensaje.
    [Fact]
    public async Task EstamparSedeCuandoSedeDeMarcacionResuelta_LogueaWarning_CuandoSePierdeLockAlCompletar()
    {
        var lockLostException = new ServiceBusException(
            "Lock del mensaje expirado",
            ServiceBusFailureReason.MessageLockLost);
        var router = new FakePrivateEventRouter();
        var messageActions = new FakeServiceBusMessageActions(excepcionAlCompletar: lockLostException);
        var logger = new FakeLogger();
        var endpoint = new FunctionEndpoint(router, logger);

        await endpoint.Run(CrearMensaje(), messageActions, CancellationToken.None);

        messageActions.MensajeEnDeadLetter.Should().BeFalse("el lock ya no es valido, no se puede dead-letter");
        logger.WarningLogueado.Should().BeTrue();
    }

    // CA-3: el retry por reentrega lo gobierna max_delivery_count de la suscripcion, no este
    // endpoint; aqui solo se fija que un error del handler no se traga en silencio.
    [Fact]
    public async Task EstamparSedeCuandoSedeDeMarcacionResuelta_EnviaADeadLetter_CuandoOcurreErrorGenerico()
    {
        var router = new FakePrivateEventRouter(
            excepcion: new InvalidOperationException("Error inesperado en el handler"));
        var messageActions = new FakeServiceBusMessageActions();
        var logger = new FakeLogger();
        var endpoint = new FunctionEndpoint(router, logger);

        await endpoint.Run(CrearMensaje(), messageActions, CancellationToken.None);

        messageActions.MensajeEnDeadLetter.Should().BeTrue();
        messageActions.MensajeCompletado.Should().BeFalse();
    }
}

internal class FakePrivateEventRouter : IPrivateEventRouter
{
    private readonly Exception? _excepcion;

    public FakePrivateEventRouter(Exception? excepcion = null)
    {
        _excepcion = excepcion;
    }

    public Task InvokeAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : class, IPrivateEvent
    {
        if (_excepcion is not null)
            throw _excepcion;
        return Task.CompletedTask;
    }
}

internal class FakeServiceBusMessageActions : ServiceBusMessageActions
{
    private readonly Exception? _excepcionAlCompletar;

    public bool MensajeCompletado { get; private set; }
    public bool MensajeEnDeadLetter { get; private set; }

    public FakeServiceBusMessageActions(Exception? excepcionAlCompletar = null)
    {
        _excepcionAlCompletar = excepcionAlCompletar;
    }

    public override Task CompleteMessageAsync(
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken = default)
    {
        if (_excepcionAlCompletar is not null)
            throw _excepcionAlCompletar;
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

internal class FakeLogger : ILogger<FunctionEndpoint>
{
    public bool WarningLogueado { get; private set; }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (logLevel == LogLevel.Warning)
            WarningLogueado = true;
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
}
