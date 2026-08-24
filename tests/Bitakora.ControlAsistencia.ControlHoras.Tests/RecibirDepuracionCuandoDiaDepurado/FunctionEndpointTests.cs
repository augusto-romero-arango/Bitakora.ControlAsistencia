// Issue #425: Tests del FunctionEndpoint del ServiceBus trigger RecibirDepuracionCuandoDiaDepurado.
// MEF-ADR-0024 decision #3 + #8: DiaDepurado cruza fisicamente el ASB interno del BC, aun siendo
// consumido dentro del mismo BC; el evento se despacha directo al IPrivateEventRouter (sin comando
// espejo). Verifica orquestacion: deserializacion + despacho al private event router + manejo de
// errores de Service Bus (patron identico a AdicionarMarcacionCuandoRegistroDeMarcacionCreado).

using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.ControlHoras.RecibirDepuracionCuandoDiaDepurado;
using Cosmos.EventDriven.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RecibirDepuracionCuandoDiaDepurado;

public class FunctionEndpointTests
{
    // JSON en formato camelCase - Wolverine serializa con camelCase por defecto al publicar al
    // Service Bus. Dia sin jornada valida (Colaborador/NombreTurno null, Franjas y
    // HorasPorConcepto vacios): el caso minimo valido del contrato de DiaDepurado.
    private const string JsonFormatoWolverine = """
        {
          "codigoColaborador": "EMP-001",
          "fecha": "2026-03-15",
          "colaborador": null,
          "nombreTurno": null,
          "franjas": [],
          "marcaciones": [],
          "horasDiscriminadas": { "horasPorConcepto": {}, "trazabilidad": [] }
        }
        """;

    private static ServiceBusReceivedMessage CrearMensaje()
        => ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString(JsonFormatoWolverine));

    // CA-1: camino feliz - deserializa el JSON, despacha al private event router, completa el mensaje
    [Fact]
    public async Task RecibirDepuracionCuandoDiaDepurado_CompletaMensaje_CuandoProcesamientoEsExitoso()
    {
        var router = new FakePrivateEventRouter();
        var messageActions = new FakeServiceBusMessageActions();
        var logger = new FakeLogger();
        var endpoint = new FunctionEndpoint(router, logger);

        await endpoint.Run(CrearMensaje(), messageActions, CancellationToken.None);

        messageActions.MensajeCompletado.Should().BeTrue();
        messageActions.MensajeEnDeadLetter.Should().BeFalse();
    }

    // Lock perdido al intentar completar -> log warning, NO dead-letter. El Service Bus re-entregara
    // el mensaje automaticamente (regresion del issue #48, ya cubierta en los demas endpoints).
    [Fact]
    public async Task RecibirDepuracionCuandoDiaDepurado_LogueaWarning_CuandoSePierdeLockAlCompletar()
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

    // Error generico durante el procesamiento -> dead-letter el mensaje para inspeccion
    [Fact]
    public async Task RecibirDepuracionCuandoDiaDepurado_EnviaADeadLetter_CuandoOcurreErrorGenerico()
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
// Duplicado deliberado del mismo patron que AdicionarMarcacionCuandoRegistroDeMarcacionCreado.
// FunctionEndpointTests (MEF-ADR-0018 Rule of Three: sin ensamblado compartido de test doubles
// entre feature folders todavia).

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
