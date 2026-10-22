using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.ControlHoras.RepartirMarcacionesCuandoDiaDepuradoFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RepartirMarcacionesCuandoDiaDepuradoFunction;

public class FunctionEndpointTests
{
    private const string JsonDiaDepurado = """
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

    private static ServiceBusReceivedMessage CrearMensaje(string json = JsonDiaDepurado)
        => ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString(json));

    [Fact]
    public async Task RepartirMarcacionesCuandoDiaDepurado_EnviaElComandoYCompleta_CuandoProcesamientoEsExitoso()
    {
        var router = new FakeRouterRepartir();
        var acciones = new FakeAccionesRepartir();
        var endpoint = new FunctionEndpoint(router, new FakeLogger());

        await endpoint.Run(CrearMensaje(), acciones, CancellationToken.None);

        router.Comandos.Should().Equal(new RepartirMarcaciones("EMP-001", new DateOnly(2026, 3, 15)));
        acciones.MensajeCompletado.Should().BeTrue();
        acciones.MensajeEnDeadLetter.Should().BeFalse();
    }

    [Fact]
    public async Task RepartirMarcacionesCuandoDiaDepurado_LogueaWarning_CuandoSePierdeLockAlCompletar()
    {
        var lockLost = new ServiceBusException("Lock expirado", ServiceBusFailureReason.MessageLockLost);
        var acciones = new FakeAccionesRepartir(excepcionAlCompletar: lockLost);
        var logger = new FakeLogger();
        var endpoint = new FunctionEndpoint(new FakeRouterRepartir(), logger);

        await endpoint.Run(CrearMensaje(), acciones, CancellationToken.None);

        acciones.MensajeEnDeadLetter.Should().BeFalse();
        logger.WarningLogueado.Should().BeTrue();
    }

    [Fact]
    public async Task RepartirMarcacionesCuandoDiaDepurado_EnviaADeadLetter_CuandoElHandlerFalla()
    {
        var router = new FakeRouterRepartir(new InvalidOperationException("fallo"));
        var acciones = new FakeAccionesRepartir();
        var endpoint = new FunctionEndpoint(router, new FakeLogger());

        await endpoint.Run(CrearMensaje(), acciones, CancellationToken.None);

        acciones.MensajeEnDeadLetter.Should().BeTrue();
        acciones.MensajeCompletado.Should().BeFalse();
    }

    [Fact]
    public async Task RepartirMarcacionesCuandoDiaDepurado_EnviaADeadLetter_CuandoElCuerpoNoEsDeserializable()
    {
        var router = new FakeRouterRepartir();
        var acciones = new FakeAccionesRepartir();
        var endpoint = new FunctionEndpoint(router, new FakeLogger());

        await endpoint.Run(CrearMensaje("no es json"), acciones, CancellationToken.None);

        router.Comandos.Should().BeEmpty();
        acciones.MensajeEnDeadLetter.Should().BeTrue();
    }
}

internal class FakeRouterRepartir(Exception? excepcion = null) : ICommandRouter
{
    public List<object> Comandos { get; } = [];

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (excepcion is not null) throw excepcion;
        Comandos.Add(command);
        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}

internal class FakeAccionesRepartir(Exception? excepcionAlCompletar = null) : ServiceBusMessageActions
{
    public bool MensajeCompletado { get; private set; }
    public bool MensajeEnDeadLetter { get; private set; }

    public override Task CompleteMessageAsync(
        ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
    {
        if (excepcionAlCompletar is not null) throw excepcionAlCompletar;
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
}

internal class FakeLogger : ILogger<FunctionEndpoint>
{
    public bool WarningLogueado { get; private set; }

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (logLevel == LogLevel.Warning) WarningLogueado = true;
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
}
