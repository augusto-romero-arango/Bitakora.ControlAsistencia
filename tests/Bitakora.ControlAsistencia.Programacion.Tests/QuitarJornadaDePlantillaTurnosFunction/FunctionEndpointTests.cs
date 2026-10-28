using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.QuitarJornadaDePlantillaTurnosFunction;
using Bitakora.ControlAsistencia.Programacion.QuitarJornadaDePlantillaTurnosFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.Tests.Soporte;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.Tests.QuitarJornadaDePlantillaTurnosFunction;

public class FunctionEndpointTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000001284");

    private static HttpRequest Request() => new DefaultHttpContext().Request;

    [Fact]
    public void QuitarJornadaDePlantillaTurnos_ExponeElVerboYLaRutaPactadosEnElIssue()
    {
        var metodo = typeof(FunctionEndpoint).GetMethod(nameof(FunctionEndpoint.Run))!;
        metodo.GetCustomAttribute<FunctionAttribute>()!.Name.Should().Be("QuitarJornadaDePlantillaTurnos");
        var trigger = metodo.GetParameters().Select(p => p.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(a => a is not null)!;

        trigger.Methods.Should().Equal("delete");
        trigger.Route.Should().Be("programacion/plantillas-turnos/{id}/jornada");
    }

    [Fact]
    public async Task QuitarJornadaDePlantillaTurnos_Retorna204SinCuerpo_CuandoElIdEsValido()
    {
        var router = new FakeCommandRouter();

        var resultado = await new FunctionEndpoint(router)
            .Run(Request(), PlantillaId.ToString(), CancellationToken.None);

        resultado.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        resultado.Should().NotBeAssignableTo<ObjectResult>();
        router.Invocado.Should().BeTrue();
    }

    [Fact]
    public async Task QuitarJornadaDePlantillaTurnos_Retorna400SinInvocarElRouter_CuandoElIdNoEsGuid()
    {
        var router = new FakeCommandRouter();

        var resultado = await new FunctionEndpoint(router).Run(Request(), "no-es-guid", CancellationToken.None);

        resultado.Should().BeOfType<BadRequestObjectResult>();
        router.Invocado.Should().BeFalse();
    }

    [Fact]
    public async Task QuitarJornadaDePlantillaTurnos_Retorna404_CuandoElRouterLanzaRecursoNoEncontradoException()
    {
        var router = new FakeCommandRouter(new RecursoNoEncontradoException(
            QuitarJornadaDePlantillaTurnosCommandHandler.Mensajes.PlantillaNoEncontrada));

        var resultado = await new FunctionEndpoint(router)
            .Run(Request(), PlantillaId.ToString(), CancellationToken.None);

        resultado.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task QuitarJornadaDePlantillaTurnos_Retorna409_CuandoElRouterLanzaReglaDeNegocioDeclinadaException()
    {
        var router = new FakeCommandRouter(new ReglaDeNegocioDeclinadaException(
            QuitarJornadaDePlantillaTurnosCommandHandler.Mensajes.PlantillaRetirada));

        var resultado = await new FunctionEndpoint(router)
            .Run(Request(), PlantillaId.ToString(), CancellationToken.None);

        resultado.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task QuitarJornadaDePlantillaTurnos_PropagaInvalidOperationException_CuandoFallaLaInfraestructura()
    {
        var fallo = new InvalidOperationException("Fallo de infraestructura");

        var act = async () => await new FunctionEndpoint(new FakeCommandRouter(fallo))
            .Run(Request(), PlantillaId.ToString(), CancellationToken.None);

        (await act.Should().ThrowExactlyAsync<InvalidOperationException>()).Which.Should().BeSameAs(fallo);
    }
}
