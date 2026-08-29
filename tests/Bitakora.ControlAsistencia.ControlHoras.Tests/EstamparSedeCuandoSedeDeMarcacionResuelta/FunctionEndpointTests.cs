// Issue #463: Tests del FunctionEndpoint del ServiceBus trigger
// EstamparSedeCuandoSedeDeMarcacionResuelta. Verifica orquestacion: deserializacion + despacho al
// private event router + manejo de errores de Service Bus (mismo patron que
// AdicionarMarcacionCuandoRegistroDeMarcacionCreado/FunctionEndpointTests).

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

    // CA-1: camino feliz - deserializa el JSON, despacha al private event router, completa el mensaje
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

    // Lock perdido al intentar completar -> log warning, NO dead-letter (regresion del issue #48,
    // mismo patron que AsignarTurno/AdicionarMarcacion). Service Bus re-entregara el mensaje.
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

    // CA-3: error generico durante el procesamiento (p.ej. ControlDiario/marcacion aun no existe) ->
    // dead-letter el mensaje para inspeccion. El retry real del Service Bus (abandon/redelivery, no
    // dead-letter inmediato) es responsabilidad de la politica de reintentos de la suscripcion
    // (infra), no de este endpoint -- aqui solo se verifica el camino de error generico existente.
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

// ---- Fakes manuales - NO NSubstitute ----

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
