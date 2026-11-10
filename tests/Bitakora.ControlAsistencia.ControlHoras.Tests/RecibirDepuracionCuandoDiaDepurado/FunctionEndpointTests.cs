// DiaDepurado cruza el ASB interno aun siendo consumido dentro del mismo BC y se despacha directo al
// IPrivateEventRouter, sin comando espejo (MEF-ADR-0024 decisiones #3 y #8).

using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.ControlHoras.RecibirDepuracionCuandoDiaDepurado;
using Cosmos.EventDriven.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RecibirDepuracionCuandoDiaDepurado;

public class FunctionEndpointTests
{
    // camelCase: Wolverine serializa asi al publicar al Service Bus. Dia sin jornada valida
    // (Colaborador/NombreTurno null, Franjas y HorasPorConcepto vacios): el caso minimo valido del
    // contrato de DiaDepurado.
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

    // NO dead-letter: el Service Bus re-entrega el mensaje automaticamente.
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

    [Fact]
    public async Task RecibirDepuracionCuandoDiaDepurado_AbandonaMensaje_CuandoOcurreErrorGenerico()
    {
        var router = new FakePrivateEventRouter(
            excepcion: new InvalidOperationException("Error inesperado en el handler"));
        var messageActions = new FakeServiceBusMessageActions();
        var logger = new FakeLogger();
        var endpoint = new FunctionEndpoint(router, logger);

        await endpoint.Run(CrearMensaje(), messageActions, CancellationToken.None);

        messageActions.MensajeAbandonado.Should().BeTrue();
        messageActions.MensajeEnDeadLetter.Should().BeFalse();
        messageActions.MensajeCompletado.Should().BeFalse();
    }
}

// Duplicado deliberado de los fakes de AdicionarMarcacionCuandoRegistroDeMarcacionCreado: no hay
// ensamblado compartido de test doubles entre feature folders (MEF-ADR-0018).

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
    public bool MensajeAbandonado { get; private set; }

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
