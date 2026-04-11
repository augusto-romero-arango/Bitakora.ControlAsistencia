// HU-57: Tests del FunctionEndpoint del ServiceBus trigger AsignarTurnoCuandoProgramacionTurnoDiarioSolicitada

using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction;

/// <summary>
/// Tests del endpoint ServiceBus AsignarTurnoCuandoProgramacionTurnoDiarioSolicitada.
/// Verifica orquestacion: deserializacion + despacho al command router + manejo de errores de Service Bus.
/// Regresion del issue #48: no intentar dead-letter cuando se pierde el lock.
/// </summary>
public class FunctionEndpointTests
{
    // JSON en formato camelCase - mismo JSON que ProgramacionTurnoDiarioSolicitadaDeserializacionTests (CA-5)
    // Wolverine serializa con camelCase por defecto al publicar al Service Bus.
    private const string JsonFormatoWolverine = """
        {
          "solicitudId": "019600b0-0000-7000-8000-000000000001",
          "empleado": {
            "empleadoId": "EMP-001",
            "tipoIdentificacion": "CC",
            "numeroIdentificacion": "1234567890",
            "nombres": "Luis Augusto",
            "apellidos": "Barreto"
          },
          "fecha": "2026-03-15",
          "detalleTurno": {
            "nombre": "Turno Manana",
            "franjasOrdinarias": [
              {
                "horaInicio": "08:00:00",
                "horaFin": "16:00:00",
                "diaOffsetFin": 0,
                "descansos": [],
                "extras": []
              }
            ]
          }
        }
        """;

    private static ServiceBusReceivedMessage CrearMensaje()
        => ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString(JsonFormatoWolverine));

    // CA-1: camino feliz - deserializa el JSON, despacha al command router, completa el mensaje
    [Fact]
    public async Task DebeCompletarMensaje_CuandoProcesamientoEsExitoso()
    {
        var router = new FakeCommandRouter();
        var messageActions = new FakeServiceBusMessageActions();
        var logger = new FakeLogger();
        var endpoint = new FunctionEndpoint(router, logger);

        await endpoint.Run(CrearMensaje(), messageActions, CancellationToken.None);

        messageActions.MensajeCompletado.Should().BeTrue();
        messageActions.MensajeEnDeadLetter.Should().BeFalse();
    }

    // CA-2: lock perdido al intentar completar -> log warning, NO dead-letter
    // Regresion del issue #48: el lock ya no es valido, intentar DeadLetterMessageAsync tambien fallaria.
    // El Service Bus re-entregara el mensaje automaticamente al expirar el lock.
    [Fact]
    public async Task DebeLoguearWarning_CuandoSePierdeLockAlCompletar()
    {
        var lockLostException = new ServiceBusException(
            "Lock del mensaje expirado",
            ServiceBusFailureReason.MessageLockLost);
        var router = new FakeCommandRouter();
        var messageActions = new FakeServiceBusMessageActions(excepcionAlCompletar: lockLostException);
        var logger = new FakeLogger();
        var endpoint = new FunctionEndpoint(router, logger);

        await endpoint.Run(CrearMensaje(), messageActions, CancellationToken.None);

        messageActions.MensajeEnDeadLetter.Should().BeFalse("el lock ya no es valido, no se puede dead-letter");
        logger.WarningLogueado.Should().BeTrue();
    }

    // CA-3: error generico durante el procesamiento -> dead-letter el mensaje para inspeccion
    [Fact]
    public async Task DebeEnviarADeadLetter_CuandoOcurreErrorGenerico()
    {
        var router = new FakeCommandRouter(
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

/// <summary>
/// Fake configurable de ICommandRouter. Puede completar exitosamente o lanzar
/// una excepcion especifica para simular distintos escenarios de fallo.
/// </summary>
internal class FakeCommandRouter : ICommandRouter
{
    private readonly Exception? _excepcion;

    public FakeCommandRouter(Exception? excepcion = null)
    {
        _excepcion = excepcion;
    }

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (_excepcion is not null)
            throw _excepcion;
        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}

/// <summary>
/// Fake de ServiceBusMessageActions. Registra si el mensaje fue completado o enviado a dead-letter.
/// Puede configurarse para lanzar una excepcion al completar (simulando lock perdido, CA-2).
/// </summary>
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

/// <summary>
/// Fake de ILogger[FunctionEndpoint]. Registra si se loguo algun Warning
/// para verificar el camino de lock perdido (CA-2).
/// </summary>
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
