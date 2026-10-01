using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.RetirarPlantillaSemanalFunction;
using Bitakora.ControlAsistencia.Programacion.RetirarPlantillaSemanalFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.Tests.RetirarPlantillaSemanalFunction;

public class FunctionEndpointTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000000623");

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    private static MethodInfo Run() =>
        typeof(FunctionEndpoint).GetMethod(nameof(FunctionEndpoint.Run))!;

    private static HttpTriggerAttribute Trigger() =>
        Run().GetParameters()
            .Select(parametro => parametro.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(trigger => trigger is not null)!;

    // CA-6
    [Fact]
    public void RetirarPlantillaSemanal_ExponeElVerboYLaRutaPactadosEnElIssue()
    {
        var trigger = Trigger();

        trigger.Methods.Should().Equal("delete");
        trigger.Route.Should().Be("programacion/plantillas-semanales/{id}");
    }

    // CA-6
    [Fact]
    public async Task RetirarPlantillaSemanal_Retorna204_CuandoComandoEsValido()
    {
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), PlantillaId.ToString(), CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    // El DELETE nunca es asincrono diferido, a diferencia del resto de comandos del dominio.
    [Fact]
    public async Task RetirarPlantillaSemanal_NuncaRetornaAcceptedResult_CuandoComandoEsValido()
    {
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), PlantillaId.ToString(), CancellationToken.None);

        result.Should().NotBeOfType<AcceptedResult>();
    }

    // CA-6
    [Fact]
    public async Task RetirarPlantillaSemanal_Retorna400_CuandoElIdDeRutaNoEsUnGuidValido()
    {
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), "no-es-un-guid", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.Invocado.Should().BeFalse();
    }

    // CA-6
    [Fact]
    public async Task RetirarPlantillaSemanal_Retorna404_CuandoElRouterLanzaRecursoNoEncontradoException()
    {
        var router = new FakeCommandRouter(
            new RecursoNoEncontradoException(
                RetirarPlantillaSemanalCommandHandler.Mensajes.PlantillaNoEncontrada));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), PlantillaId.ToString(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RetirarPlantillaSemanal_PropagaInvalidOperationException_CuandoFallaLaInfraestructura()
    {
        var fallo = new InvalidOperationException("Fallo de infraestructura");
        var function = new FunctionEndpoint(new FakeCommandRouter(fallo));

        var act = async () => await function.Run(
            FakeHttpRequest(), PlantillaId.ToString(), CancellationToken.None);

        (await act.Should().ThrowExactlyAsync<InvalidOperationException>()).Which.Should().BeSameAs(fallo);
    }

    [Fact]
    public async Task RetirarPlantillaSemanal_PropagaReglaDeNegocioDeclinadaException_CuandoLaDerivadaNoEstaMapeada()
    {
        var fallo = new ReglaDeNegocioDeclinadaException(
            RetirarPlantillaSemanalCommandHandler.Mensajes.PlantillaNoEncontrada);
        var function = new FunctionEndpoint(new FakeCommandRouter(fallo));

        var act = async () => await function.Run(
            FakeHttpRequest(), PlantillaId.ToString(), CancellationToken.None);

        (await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>())
            .Which.Should().BeSameAs(fallo);
    }
}

// Fake manual - NO NSubstitute.
internal sealed class FakeCommandRouter(Exception? excepcion = null) : ICommandRouter
{
    public bool Invocado { get; private set; }

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        Invocado = true;
        if (excepcion is not null) throw excepcion;
        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default)
        where TCommand : class =>
        throw new NotImplementedException();
}
