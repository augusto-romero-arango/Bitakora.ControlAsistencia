using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class ServiceBusSessionEndpointBaseTests
{
    private static ServiceBusReceivedMessage CrearMensaje(
        string messageId = "msg-1", string sessionId = "EMP-001", int deliveryCount = 1)
        => ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"), messageId: messageId, sessionId: sessionId,
            deliveryCount: deliveryCount, subject: "Cualquiera");

    [Fact]
    public async Task ProcesarMensajeDeSesion_CompletaElMensaje_CuandoLaInvocacionEsExitosa()
    {
        var actions = new FakeServiceBusMessageActions();
        var endpoint = new StubEndpointDeSesion(_ => Task.CompletedTask);

        await endpoint.Procesar(CrearMensaje(), actions, CancellationToken.None);

        actions.MensajeCompletado.Should().BeTrue();
        actions.MensajeEnDeadLetter.Should().BeFalse();
        actions.MensajeAbandonado.Should().BeFalse();
    }

    [Fact]
    public async Task ProcesarMensajeDeSesion_EnviaADeadLetterSinInvocar_CuandoLaInterpretacionFalla()
    {
        var invocado = false;
        var actions = new FakeServiceBusMessageActions();
        var endpoint = new StubEndpointDeSesion(
            _ => { invocado = true; return Task.CompletedTask; },
            fallaInterpretacion: new InvalidOperationException("Subject desconocido"));

        await endpoint.Procesar(CrearMensaje(), actions, CancellationToken.None);

        actions.MensajeEnDeadLetter.Should().BeTrue();
        actions.MensajeAbandonado.Should().BeFalse();
        actions.MensajeCompletado.Should().BeFalse();
        invocado.Should().BeFalse();
    }

    [Fact]
    public async Task ProcesarMensajeDeSesion_AbandonaElMensaje_CuandoLaInvocacionFalla()
    {
        var actions = new FakeServiceBusMessageActions();
        var endpoint = new StubEndpointDeSesion(
            _ => throw new InvalidOperationException("Error de infraestructura"));

        await endpoint.Procesar(CrearMensaje(), actions, CancellationToken.None);

        actions.MensajeAbandonado.Should().BeTrue();
        actions.MensajeEnDeadLetter.Should().BeFalse();
        actions.MensajeCompletado.Should().BeFalse();
    }

    [Fact]
    public async Task ProcesarMensajeDeSesion_SoloRegistraWarning_CuandoSePierdeElLockAlCompletar()
    {
        var lockLost = new ServiceBusException("Lock expirado", ServiceBusFailureReason.MessageLockLost);
        var actions = new FakeServiceBusMessageActions(excepcionAlCompletar: lockLost);
        var logger = new FakeLogger();
        var endpoint = new StubEndpointDeSesion(_ => Task.CompletedTask, logger: logger);

        await endpoint.Procesar(CrearMensaje(), actions, CancellationToken.None);

        actions.MensajeAbandonado.Should().BeFalse();
        actions.MensajeEnDeadLetter.Should().BeFalse();
        logger.WarningLogueado.Should().BeTrue();
    }

    [Fact]
    public async Task ProcesarMensajeDeSesion_SoloRegistraWarning_CuandoLaInvocacionPierdeElLock()
    {
        var lockLost = new ServiceBusException("Lock expirado", ServiceBusFailureReason.MessageLockLost);
        var actions = new FakeServiceBusMessageActions();
        var logger = new FakeLogger();
        var endpoint = new StubEndpointDeSesion(_ => throw lockLost, logger: logger);

        await endpoint.Procesar(CrearMensaje(), actions, CancellationToken.None);

        actions.MensajeAbandonado.Should().BeFalse();
        actions.MensajeEnDeadLetter.Should().BeFalse();
        logger.WarningLogueado.Should().BeTrue();
    }

    [Fact]
    public async Task ProcesarMensajeDeSesion_RegistraMessageIdSessionIdYDeliveryCount_CuandoAbandona()
    {
        var logger = new FakeLogger();
        var endpoint = new StubEndpointDeSesion(_ => throw new InvalidOperationException("x"), logger: logger);

        await endpoint.Procesar(
            CrearMensaje("msg-1175", "EMP-777", 5), new FakeServiceBusMessageActions(), CancellationToken.None);

        logger.MensajesError.Should().ContainSingle()
            .Which.Should().Contain("msg-1175").And.Contain("EMP-777").And.Contain("5");
    }

    [Fact]
    public async Task ProcesarMensajeDeSesion_RegistraMessageIdSessionIdYDeliveryCount_CuandoEnviaADeadLetter()
    {
        var logger = new FakeLogger();
        var endpoint = new StubEndpointDeSesion(
            _ => Task.CompletedTask, fallaInterpretacion: new InvalidOperationException("x"), logger: logger);

        await endpoint.Procesar(
            CrearMensaje("msg-1175", "EMP-777", 2), new FakeServiceBusMessageActions(), CancellationToken.None);

        logger.MensajesError.Should().ContainSingle()
            .Which.Should().Contain("msg-1175").And.Contain("EMP-777").And.Contain("2");
    }

    [Fact]
    public async Task ProcesarMensajeDeSesion_RegistraMessageIdYSessionId_CuandoSePierdeElLock()
    {
        var lockLost = new ServiceBusException("Lock expirado", ServiceBusFailureReason.MessageLockLost);
        var logger = new FakeLogger();
        var endpoint = new StubEndpointDeSesion(_ => Task.CompletedTask, logger: logger);

        await endpoint.Procesar(
            CrearMensaje("msg-1175", "EMP-777", 2),
            new FakeServiceBusMessageActions(excepcionAlCompletar: lockLost), CancellationToken.None);

        logger.MensajesWarning.Should().ContainSingle()
            .Which.Should().Contain("msg-1175").And.Contain("EMP-777");
    }
}

internal class StubEndpointDeSesion(
    Func<CancellationToken, Task> invocacion,
    Exception? fallaInterpretacion = null,
    ILogger? logger = null)
    : ServiceBusSessionEndpointBase(logger ?? new FakeLogger())
{
    public Task Procesar(
        ServiceBusReceivedMessage message, ServiceBusMessageActions actions, CancellationToken ct)
        => ProcesarMensajeDeSesion(message, actions, ct);

    protected override Func<CancellationToken, Task> Interpretar(ServiceBusReceivedMessage message)
        => fallaInterpretacion is null ? invocacion : throw fallaInterpretacion;
}
