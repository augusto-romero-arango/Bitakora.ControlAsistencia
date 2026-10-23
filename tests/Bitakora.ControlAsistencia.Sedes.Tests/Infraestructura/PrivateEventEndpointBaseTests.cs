using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using JasperFx.Events;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.Sedes.Tests.Infraestructura;

public class PrivateEventEndpointBaseTests
{
    private const string JsonValido = """{"nombre": "test"}""";

    private static ServiceBusReceivedMessage CrearMensaje(string json = JsonValido)
        => ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString(json));

    [Fact]
    public async Task ProcesarMensaje_CompletaElMensaje_CuandoElHandlerTieneExito()
    {
        var router = new RouterFalso();
        var actions = new AccionesFalsas();
        var endpoint = new EndpointStub(router, new LoggerFalso());

        await endpoint.Procesar(CrearMensaje(), actions, CancellationToken.None);

        actions.Completado.Should().BeTrue();
        actions.EnDeadLetter.Should().BeFalse();
        actions.Abandonado.Should().BeFalse();
    }

    [Fact]
    public async Task ProcesarMensaje_AbandonaElMensaje_CuandoElHandlerFalla()
    {
        var router = new RouterFalso(new InvalidOperationException("Error inesperado"));
        var actions = new AccionesFalsas();
        var endpoint = new EndpointStub(router, new LoggerFalso());

        await endpoint.Procesar(CrearMensaje(), actions, CancellationToken.None);

        actions.Abandonado.Should().BeTrue();
        actions.EnDeadLetter.Should().BeFalse();
        actions.Completado.Should().BeFalse();
    }

    [Fact]
    public async Task ProcesarMensaje_AbandonaElMensaje_CuandoHayConflictoDeConcurrencia()
    {
        var router = new RouterFalso(
            new EventStreamUnexpectedMaxEventIdException("p:stream", typeof(object), 2, 3));
        var actions = new AccionesFalsas();
        var endpoint = new EndpointStub(router, new LoggerFalso());

        await endpoint.Procesar(CrearMensaje(), actions, CancellationToken.None);

        actions.Abandonado.Should().BeTrue();
        actions.EnDeadLetter.Should().BeFalse();
    }

    [Fact]
    public async Task ProcesarMensaje_EnviaADeadLetterSinInvocarElHandler_CuandoElCuerpoDeserializaANull()
    {
        var router = new RouterFalso();
        var actions = new AccionesFalsas();
        var endpoint = new EndpointStub(router, new LoggerFalso());

        await endpoint.Procesar(CrearMensaje("null"), actions, CancellationToken.None);

        actions.EnDeadLetter.Should().BeTrue();
        actions.Abandonado.Should().BeFalse();
        router.Invocaciones.Should().Be(0);
    }

    [Fact]
    public async Task ProcesarMensaje_EnviaADeadLetterSinInvocarElHandler_CuandoElJsonEsInvalido()
    {
        var router = new RouterFalso();
        var actions = new AccionesFalsas();
        var endpoint = new EndpointStub(router, new LoggerFalso());

        await endpoint.Procesar(CrearMensaje("no-es-json"), actions, CancellationToken.None);

        actions.EnDeadLetter.Should().BeTrue();
        actions.Abandonado.Should().BeFalse();
        router.Invocaciones.Should().Be(0);
    }

    [Fact]
    public async Task ProcesarMensaje_SoloRegistraWarning_CuandoSePierdeElLock()
    {
        var lockLost = new ServiceBusException("Lock expirado", ServiceBusFailureReason.MessageLockLost);
        var actions = new AccionesFalsas(excepcionAlCompletar: lockLost);
        var logger = new LoggerFalso();
        var endpoint = new EndpointStub(new RouterFalso(), logger);

        await endpoint.Procesar(CrearMensaje(), actions, CancellationToken.None);

        actions.EnDeadLetter.Should().BeFalse();
        actions.Abandonado.Should().BeFalse();
        logger.WarningLogueado.Should().BeTrue();
    }

    [Fact]
    public async Task ProcesarMensaje_RegistraMessageIdYDeliveryCount_CuandoAbandonaElMensaje()
    {
        var router = new RouterFalso(new InvalidOperationException("Error"));
        var logger = new LoggerFalso();
        var endpoint = new EndpointStub(router, logger);
        var mensaje = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(JsonValido), messageId: "msg-1177", deliveryCount: 3);

        await endpoint.Procesar(mensaje, new AccionesFalsas(), CancellationToken.None);

        logger.MensajesError.Should().ContainSingle()
            .Which.Should().Contain("msg-1177").And.Contain("3");
    }

    private record EventoStub(string? Nombre) : IPrivateEvent;

    private class EndpointStub(IPrivateEventRouter router, ILogger logger)
        : PrivateEventEndpointBase<EventoStub>(router, logger)
    {
        public Task Procesar(
            ServiceBusReceivedMessage message, ServiceBusMessageActions actions, CancellationToken ct)
            => ProcesarMensaje(message, actions, ct);
    }

    private class RouterFalso(Exception? excepcion = null) : IPrivateEventRouter
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

    private class AccionesFalsas(Exception? excepcionAlCompletar = null) : ServiceBusMessageActions
    {
        public bool Completado { get; private set; }
        public bool EnDeadLetter { get; private set; }
        public bool Abandonado { get; private set; }

        public override Task CompleteMessageAsync(
            ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
        {
            if (excepcionAlCompletar is not null) throw excepcionAlCompletar;
            Completado = true;
            return Task.CompletedTask;
        }

        public override Task DeadLetterMessageAsync(
            ServiceBusReceivedMessage message,
            Dictionary<string, object>? propertiesToModify = null,
            string? deadLetterReason = null,
            string? deadLetterErrorDescription = null,
            CancellationToken cancellationToken = default)
        {
            EnDeadLetter = true;
            return Task.CompletedTask;
        }

        public override Task AbandonMessageAsync(
            ServiceBusReceivedMessage message,
            IDictionary<string, object>? propertiesToModify = null,
            CancellationToken cancellationToken = default)
        {
            Abandonado = true;
            return Task.CompletedTask;
        }
    }

    private class LoggerFalso : ILogger
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
}
