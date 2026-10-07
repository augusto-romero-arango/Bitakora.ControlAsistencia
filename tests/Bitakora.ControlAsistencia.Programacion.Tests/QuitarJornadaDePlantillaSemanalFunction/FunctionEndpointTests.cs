using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.QuitarJornadaDePlantillaSemanalFunction;
using Bitakora.ControlAsistencia.Programacion.QuitarJornadaDePlantillaSemanalFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.Tests.AsignarTurnoADiaDePlantillaSemanalFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.Tests.QuitarJornadaDePlantillaSemanalFunction;

public class FunctionEndpointTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000000867");

    private static HttpRequest Request() => new DefaultHttpContext().Request;

    [Fact]
    public void QuitarJornadaDePlantillaSemanal_ExponeElVerboYLaRutaPactadosEnElIssue()
    {
        var metodo = typeof(FunctionEndpoint).GetMethod(nameof(FunctionEndpoint.Run))!;
        metodo.GetCustomAttribute<FunctionAttribute>()!.Name.Should().Be("QuitarJornadaDePlantillaSemanal");
        var trigger = metodo.GetParameters().Select(p => p.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(a => a is not null)!;

        trigger.Methods.Should().Equal("delete");
        trigger.Route.Should().Be("programacion/plantillas-semanales/{id}/jornada");
    }

    [Fact]
    public async Task QuitarJornadaDePlantillaSemanal_Retorna204_CuandoComandoEsValido()
    {
        var router = new FakeCommandRouter();

        var resultado = await new FunctionEndpoint(router)
            .Run(Request(), PlantillaId.ToString(), CancellationToken.None);

        resultado.Should().BeOfType<NoContentResult>();
        router.Invocado.Should().BeTrue();
    }

    [Fact]
    public async Task QuitarJornadaDePlantillaSemanal_Retorna400SinInvocarElRouter_CuandoElIdNoEsGuid()
    {
        var router = new FakeCommandRouter();

        var resultado = await new FunctionEndpoint(router).Run(Request(), "no-es-guid", CancellationToken.None);

        resultado.Should().BeOfType<BadRequestObjectResult>();
        router.Invocado.Should().BeFalse();
    }

    [Fact]
    public async Task QuitarJornadaDePlantillaSemanal_Retorna404_CuandoElRouterLanzaRecursoNoEncontradoException()
    {
        var router = new FakeCommandRouter(new RecursoNoEncontradoException(
            QuitarJornadaDePlantillaSemanalCommandHandler.Mensajes.PlantillaNoEncontrada));

        var resultado = await new FunctionEndpoint(router)
            .Run(Request(), PlantillaId.ToString(), CancellationToken.None);

        resultado.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task QuitarJornadaDePlantillaSemanal_Retorna409_CuandoElRouterLanzaReglaDeNegocioDeclinadaException()
    {
        var router = new FakeCommandRouter(new ReglaDeNegocioDeclinadaException(
            QuitarJornadaDePlantillaSemanalCommandHandler.Mensajes.PlantillaRetirada));

        var resultado = await new FunctionEndpoint(router)
            .Run(Request(), PlantillaId.ToString(), CancellationToken.None);

        resultado.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task QuitarJornadaDePlantillaSemanal_PropagaInvalidOperationException_CuandoFallaLaInfraestructura()
    {
        var fallo = new InvalidOperationException("Fallo de infraestructura");

        var act = async () => await new FunctionEndpoint(new FakeCommandRouter(fallo))
            .Run(Request(), PlantillaId.ToString(), CancellationToken.None);

        (await act.Should().ThrowExactlyAsync<InvalidOperationException>()).Which.Should().BeSameAs(fallo);
    }
}
