using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearPlantillaTurnosFunction;
using Bitakora.ControlAsistencia.Programacion.CrearPlantillaTurnosFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Azure.Functions.Worker;
using FakeRequestValidatorPlantillaSemanal =
    Bitakora.ControlAsistencia.Programacion.Tests.CrearPlantillaSemanalFunction.FakeRequestValidator<
        Bitakora.ControlAsistencia.Programacion.CrearPlantillaTurnosFunction.CrearPlantillaTurnos>;
using FakeRouter = Bitakora.ControlAsistencia.Programacion.Tests.CrearPlantillaSemanalFunction.FakeCommandRouter;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearPlantillaTurnosFunction;

public class FunctionEndpointTests
{
    private static CrearPlantillaTurnos ComandoValido() => new(Guid.NewGuid(), "Ciclo 8 dias", 8);

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    private static HttpTriggerAttribute Trigger() =>
        typeof(FunctionEndpoint).GetMethod(nameof(FunctionEndpoint.Run))!.GetParameters()
            .Select(p => p.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(t => t is not null)!;

    [Fact]
    public void CrearPlantillaTurnos_ExponeElVerboYLaRutaPactadosEnElIssue()
    {
        var trigger = Trigger();

        trigger.Methods.Should().Equal("post");
        trigger.Route.Should().Be("programacion/plantillas-turnos");
    }

    [Fact]
    public async Task CrearPlantillaTurnos_Retorna201ConLocation_CuandoComandoEsValido()
    {
        var comando = ComandoValido();
        var function = new FunctionEndpoint(new FakeRequestValidatorPlantillaSemanal(comando), new FakeRouter());

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
        ((CreatedResult)result).Location.Should().Be($"/api/programacion/plantillas-turnos/{comando.PlantillaId}");
    }

    [Fact]
    public async Task CrearPlantillaTurnos_NuncaRetornaAcceptedResult_CuandoComandoEsValido()
    {
        var function = new FunctionEndpoint(new FakeRequestValidatorPlantillaSemanal(ComandoValido()), new FakeRouter());

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().NotBeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task CrearPlantillaTurnos_Retorna409_CuandoPlantillaYaExiste()
    {
        var function = new FunctionEndpoint(
            new FakeRequestValidatorPlantillaSemanal(ComandoValido()),
            new FakeRouter(excepcion: new RecursoYaExisteException(
                CrearPlantillaTurnosCommandHandler.Mensajes.PlantillaYaExiste)));

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task CrearPlantillaTurnos_Retorna400_CuandoRequestEsInvalido()
    {
        var function = new FunctionEndpoint(
            new FakeRequestValidatorPlantillaSemanal(error: new BadRequestObjectResult("body invalido")),
            new FakeRouter());

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CrearPlantillaTurnos_Retorna400ConMensajes_CuandoElFactoryRechazaLosDatos()
    {
        var errores = new ArgumentException[] { new("nombre vacio"), new("dias fuera de rango") };
        var function = new FunctionEndpoint(
            new FakeRequestValidatorPlantillaSemanal(ComandoValido()),
            new FakeRouter(erroresAggregateException: errores));

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>().Which.Value
            .Should().BeAssignableTo<IEnumerable<string>>()
            .Which.Should().BeEquivalentTo(errores.Select(e => e.Message));
    }
}
