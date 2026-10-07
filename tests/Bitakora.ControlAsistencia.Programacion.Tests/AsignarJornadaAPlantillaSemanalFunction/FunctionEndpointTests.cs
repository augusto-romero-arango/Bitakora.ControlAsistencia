using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AsignarJornadaAPlantillaSemanalFunction;
using Bitakora.ControlAsistencia.Programacion.AsignarJornadaAPlantillaSemanalFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.Tests.AsignarTurnoADiaDePlantillaSemanalFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AsignarJornadaAPlantillaSemanalFunction;

public class FunctionEndpointTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000000867");
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000000a01");

    private static HttpRequest Request() => new DefaultHttpContext().Request;

    private static FunctionEndpoint Endpoint(FakeCommandRouter router,
        FakeRequestValidator<AsignarJornadaAPlantillaSemanalBody>? validator = null) =>
        new(validator ?? new FakeRequestValidator<AsignarJornadaAPlantillaSemanalBody>(
            new AsignarJornadaAPlantillaSemanalBody(JornadaId)), router);

    [Fact]
    public void AsignarJornadaAPlantillaSemanal_ExponeElVerboYLaRutaPactadosEnElIssue()
    {
        var metodo = typeof(FunctionEndpoint).GetMethod(nameof(FunctionEndpoint.Run))!;
        metodo.GetCustomAttribute<FunctionAttribute>()!.Name.Should().Be("AsignarJornadaAPlantillaSemanal");
        var trigger = metodo.GetParameters().Select(p => p.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(a => a is not null)!;

        trigger.Methods.Should().Equal("put");
        trigger.Route.Should().Be("programacion/plantillas-semanales/{id}/jornada");
    }

    [Fact]
    public async Task AsignarJornadaAPlantillaSemanal_Retorna204_CuandoComandoEsValido()
    {
        var router = new FakeCommandRouter();

        var resultado = await Endpoint(router).Run(Request(), PlantillaId.ToString(), CancellationToken.None);

        resultado.Should().BeOfType<NoContentResult>();
        router.Invocado.Should().BeTrue();
    }

    [Fact]
    public async Task AsignarJornadaAPlantillaSemanal_Retorna400SinInvocarElRouter_CuandoElIdNoEsGuid()
    {
        var router = new FakeCommandRouter();

        var resultado = await Endpoint(router).Run(Request(), "no-es-guid", CancellationToken.None);

        resultado.Should().BeOfType<BadRequestObjectResult>();
        router.Invocado.Should().BeFalse();
    }

    [Fact]
    public async Task AsignarJornadaAPlantillaSemanal_Retorna400SinInvocarElRouter_CuandoValidadorRechazaElBody()
    {
        var router = new FakeCommandRouter();
        var validator = new FakeRequestValidator<AsignarJornadaAPlantillaSemanalBody>(
            error: new BadRequestObjectResult("El body es invalido"));

        var resultado = await Endpoint(router, validator).Run(Request(), PlantillaId.ToString(), CancellationToken.None);

        resultado.Should().BeOfType<BadRequestObjectResult>();
        router.Invocado.Should().BeFalse();
    }

    [Fact]
    public async Task AsignarJornadaAPlantillaSemanal_Retorna404_CuandoElRouterLanzaRecursoNoEncontradoException()
    {
        var router = new FakeCommandRouter(new RecursoNoEncontradoException(
            AsignarJornadaAPlantillaSemanalCommandHandler.Mensajes.JornadaNoEncontrada));

        var resultado = await Endpoint(router).Run(Request(), PlantillaId.ToString(), CancellationToken.None);

        resultado.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AsignarJornadaAPlantillaSemanal_Retorna409_CuandoElRouterLanzaReglaDeNegocioDeclinadaException()
    {
        var router = new FakeCommandRouter(new ReglaDeNegocioDeclinadaException(
            AsignarJornadaAPlantillaSemanalCommandHandler.Mensajes.PlantillaRetirada));

        var resultado = await Endpoint(router).Run(Request(), PlantillaId.ToString(), CancellationToken.None);

        resultado.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task AsignarJornadaAPlantillaSemanal_PropagaInvalidOperationException_CuandoFallaLaInfraestructura()
    {
        var fallo = new InvalidOperationException("Fallo de infraestructura");

        var act = async () => await Endpoint(new FakeCommandRouter(fallo))
            .Run(Request(), PlantillaId.ToString(), CancellationToken.None);

        (await act.Should().ThrowExactlyAsync<InvalidOperationException>()).Which.Should().BeSameAs(fallo);
    }
}
