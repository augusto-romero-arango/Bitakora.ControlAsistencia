// issue #270: Tests del FunctionEndpoint del ServiceBus trigger
// AdicionarMarcacionCuandoRegistroDeMarcacionCreado (reemplaza al de #213 sobre MarcacionRegistrada,
// que dejo de implementar IPrivateEvent - CA-3).

using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoRegistroDeMarcacionCreado;
using Cosmos.EventDriven.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AdicionarMarcacionCuandoRegistroDeMarcacionCreado;

/// <summary>
/// Tests del endpoint ServiceBus AdicionarMarcacionCuandoRegistroDeMarcacionCreado.
/// ADR-0024 (marco) decision #3 + #8: RegistroDeMarcacionCreado cruza fisicamente el ASB interno del
/// BC; el evento se despacha directo al IPrivateEventRouter (sin comando espejo).
/// Verifica orquestacion: deserializacion + despacho al private event router + manejo de
/// errores de Service Bus (patron identico a AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction).
/// </summary>
public class FunctionEndpointTests
{
    // JSON en formato camelCase - Wolverine serializa con camelCase por defecto al publicar al Service Bus.
    // Mismos campos que RegistroDeMarcacionCreadoDeserializacionTests (EmpleadoId, TimestampNormalizado,
    // TipoMarcacion, DispositivoId); RegistroDeMarcacionCreado es un record con constructor primario
    // publico, por lo que ServiceBusDeserializador (case-insensitive) lo resuelve via ese constructor.
    private const string JsonFormatoWolverine = """
        {
          "empleadoId": "EMP-001",
          "timestampNormalizado": "2026-03-15T08:09:00",
          "tipoMarcacion": "ENTRADA",
          "dispositivoId": "DEV-001"
        }
        """;

    private static ServiceBusReceivedMessage CrearMensaje()
        => ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString(JsonFormatoWolverine));

    // CA-5: camino feliz - deserializa el JSON, despacha al private event router, completa el mensaje
    [Fact]
    public async Task AdicionarMarcacionCuandoRegistroDeMarcacionCreado_CompletaMensaje_CuandoProcesamientoEsExitoso()
    {
        var router = new FakePrivateEventRouter();
        var messageActions = new FakeServiceBusMessageActions();
        var logger = new FakeLogger();
        var endpoint = new FunctionEndpoint(router, logger);

        await endpoint.Run(CrearMensaje(), messageActions, CancellationToken.None);

        messageActions.MensajeCompletado.Should().BeTrue();
        messageActions.MensajeEnDeadLetter.Should().BeFalse();
    }

    // CA-5: lock perdido al intentar completar -> log warning, NO dead-letter
    // Regresion del issue #48 (ya cubierta en AsignarTurno): el lock ya no es valido, intentar
    // DeadLetterMessageAsync tambien fallaria. El Service Bus re-entregara el mensaje automaticamente.
    [Fact]
    public async Task AdicionarMarcacionCuandoRegistroDeMarcacionCreado_LogueaWarning_CuandoSePierdeLockAlCompletar()
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

    // CA-5: error generico durante el procesamiento -> dead-letter el mensaje para inspeccion
    [Fact]
    public async Task AdicionarMarcacionCuandoRegistroDeMarcacionCreado_EnviaADeadLetter_CuandoOcurreErrorGenerico()
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

/// <summary>
/// Fake configurable de IPrivateEventRouter. Puede despachar exitosamente o lanzar
/// una excepcion especifica para simular distintos escenarios de fallo.
/// </summary>
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

/// <summary>
/// Fake de ServiceBusMessageActions. Registra si el mensaje fue completado o enviado a dead-letter.
/// Puede configurarse para lanzar una excepcion al completar (simulando lock perdido).
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
/// para verificar el camino de lock perdido.
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
