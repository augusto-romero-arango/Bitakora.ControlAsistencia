using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.Tests.CrearPlantillaSemanalFunction;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearJornadaFunction;

public class FunctionEndpointTests
{
    private static CrearJornada Comando() => new(Guid.NewGuid(), new(42, 0), new(8, 0), new(0, 0), 1);
    private static HttpRequest Request() => new DefaultHttpContext().Request;

    [Fact]
    public void CrearJornada_ExponePostEnRutaJornadas()
    {
        var metodo = typeof(FunctionEndpoint).GetMethod(nameof(FunctionEndpoint.Run))!;
        metodo.GetCustomAttribute<FunctionAttribute>()!.Name.Should().Be("CrearJornada");
        var trigger = metodo.GetParameters().Select(p => p.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(a => a is not null)!;
        trigger.Methods.Should().Equal("post");
        trigger.Route.Should().Be("programacion/jornadas");
    }

    [Fact]
    public async Task CrearJornada_Retorna201ConLocation_CuandoComandoEsValido()
    {
        var comando = Comando();
        var endpoint = new FunctionEndpoint(new FakeRequestValidator<CrearJornada>(comando), new FakeCommandRouter());
        var resultado = await endpoint.Run(Request(), CancellationToken.None);
        resultado.Should().BeAssignableTo<IStatusCodeActionResult>().Which.StatusCode.Should().Be(StatusCodes.Status201Created);
        ((CreatedResult)resultado).Location.Should().Be($"/api/programacion/jornadas/{comando.JornadaId}");
    }

    [Fact]
    public async Task CrearJornada_Retorna400_CuandoValidadorRechazaLaSolicitud()
    {
        var endpoint = new FunctionEndpoint(new FakeRequestValidator<CrearJornada>(
            error: new BadRequestObjectResult(CrearJornadaCommandHandler.Mensajes.JornadaYaExiste)),
            new FakeCommandRouter());
        var resultado = await endpoint.Run(Request(), CancellationToken.None);
        resultado.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CrearJornada_Retorna400ConTodosLosMensajes_CuandoFactoryRechazaLimites()
    {
        var endpoint = new FunctionEndpoint(new FakeRequestValidator<CrearJornada>(Comando()),
            new FakeCommandRouter(erroresAggregateException:
            [new ArgumentException(Bitakora.ControlAsistencia.Programacion.DomainEvents.HorasYMinutos.Mensajes.MinutosFueraDeRango),
             new ArgumentException(Bitakora.ControlAsistencia.Programacion.DomainEvents.LimitesJornada.Mensajes.DescansosFueraDeRango)]));
        var resultado = await endpoint.Run(Request(), CancellationToken.None);
        var mensajes = resultado.Should().BeOfType<BadRequestObjectResult>().Which.Value;
        mensajes.Should().BeAssignableTo<IEnumerable<string>>().Which.Should().BeEquivalentTo(new[]
        {
            Bitakora.ControlAsistencia.Programacion.DomainEvents.HorasYMinutos.Mensajes.MinutosFueraDeRango,
            Bitakora.ControlAsistencia.Programacion.DomainEvents.LimitesJornada.Mensajes.DescansosFueraDeRango
        });
    }

    [Fact]
    public async Task CrearJornada_Retorna409_CuandoIdYaExiste()
    {
        var endpoint = new FunctionEndpoint(new FakeRequestValidator<CrearJornada>(Comando()),
            new FakeCommandRouter(excepcion: new RecursoYaExisteException(
                CrearJornadaCommandHandler.Mensajes.JornadaYaExiste)));
        var resultado = await endpoint.Run(Request(), CancellationToken.None);
        resultado.Should().BeOfType<ConflictObjectResult>().Which.Value.Should()
            .Be(CrearJornadaCommandHandler.Mensajes.JornadaYaExiste);
    }
}
