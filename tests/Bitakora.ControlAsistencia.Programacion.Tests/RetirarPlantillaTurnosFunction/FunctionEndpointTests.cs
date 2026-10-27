using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.RetirarPlantillaTurnosFunction;
using Bitakora.ControlAsistencia.Programacion.RetirarPlantillaTurnosFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.Tests.RetirarPlantillaTurnosFunction;

public class FunctionEndpointTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000001283");

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    private static MethodInfo Run() =>
        typeof(FunctionEndpoint).GetMethod(nameof(FunctionEndpoint.Run))!;

    private static HttpTriggerAttribute Trigger() =>
        Run().GetParameters()
            .Select(parametro => parametro.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(trigger => trigger is not null)!;

    [Fact]
    public void RetirarPlantillaTurnos_ExponeElVerboYLaRutaPactadosEnElIssue()
    {
        var trigger = Trigger();

        trigger.Methods.Should().Equal("delete");
        trigger.Route.Should().Be("programacion/plantillas-turnos/{id}");
    }

    [Fact]
    public async Task RetirarPlantillaTurnos_Retorna204_CuandoComandoEsValido()
    {
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), PlantillaId.ToString(), CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task RetirarPlantillaTurnos_NuncaRetornaAcceptedResult_CuandoComandoEsValido()
    {
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), PlantillaId.ToString(), CancellationToken.None);

        result.Should().NotBeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task RetirarPlantillaTurnos_Retorna400_CuandoElIdDeRutaNoEsUnGuidValido()
    {
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), "no-es-un-guid", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.Invocado.Should().BeFalse();
    }

    [Fact]
    public async Task RetirarPlantillaTurnos_Retorna404_CuandoElRouterLanzaRecursoNoEncontradoException()
    {
        var router = new FakeCommandRouter(
            new RecursoNoEncontradoException(
                RetirarPlantillaTurnosCommandHandler.Mensajes.PlantillaNoEncontrada));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), PlantillaId.ToString(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RetirarPlantillaTurnos_PropagaInvalidOperationException_CuandoFallaLaInfraestructura()
    {
        var fallo = new InvalidOperationException("Fallo de infraestructura");
        var function = new FunctionEndpoint(new FakeCommandRouter(fallo));

        var act = async () => await function.Run(
            FakeHttpRequest(), PlantillaId.ToString(), CancellationToken.None);

        (await act.Should().ThrowExactlyAsync<InvalidOperationException>()).Which.Should().BeSameAs(fallo);
    }

    [Fact]
    public async Task RetirarPlantillaTurnos_PropagaReglaDeNegocioDeclinadaException_CuandoLaDerivadaNoEstaMapeada()
    {
        var fallo = new ReglaDeNegocioDeclinadaException(
            RetirarPlantillaTurnosCommandHandler.Mensajes.PlantillaNoEncontrada);
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
