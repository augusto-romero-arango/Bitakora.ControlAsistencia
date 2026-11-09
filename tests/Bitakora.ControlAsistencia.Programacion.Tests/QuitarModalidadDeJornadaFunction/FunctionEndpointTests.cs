using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.QuitarModalidadDeJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.QuitarModalidadDeJornadaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.Tests.Soporte;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.Tests.QuitarModalidadDeJornadaFunction;

public class FunctionEndpointTests
{
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000001436");

    private static HttpRequest Request() => new DefaultHttpContext().Request;

    [Fact]
    public void QuitarModalidadDeJornada_ExponeDeleteEnRutaModalidadDeLaJornada()
    {
        var metodo = typeof(FunctionEndpoint).GetMethod(nameof(FunctionEndpoint.Run))!;
        metodo.GetCustomAttribute<FunctionAttribute>()!.Name.Should().Be("QuitarModalidadDeJornada");
        var trigger = metodo.GetParameters().Select(p => p.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(a => a is not null)!;
        trigger.Methods.Should().Equal("delete");
        trigger.Route.Should().Be("programacion/jornadas/{id}/modalidad");
    }

    [Fact]
    public async Task QuitarModalidadDeJornada_Retorna204SinCuerpo_CuandoElIdEsValido()
    {
        var router = new FakeCommandRouter();
        var resultado = await new FunctionEndpoint(router)
            .Run(Request(), JornadaId.ToString(), CancellationToken.None);
        resultado.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        resultado.Should().NotBeAssignableTo<ObjectResult>();
        router.Invocado.Should().BeTrue();
    }

    [Fact]
    public async Task QuitarModalidadDeJornada_Retorna204_CuandoLaJornadaYaNoTieneModalidad()
    {
        var router = new FakeCommandRouter();
        var resultado = await new FunctionEndpoint(router)
            .Run(Request(), JornadaId.ToString(), CancellationToken.None);
        resultado.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
    }

    [Fact]
    public async Task QuitarModalidadDeJornada_Retorna400SinInvocarElRouter_CuandoElIdNoEsGuid()
    {
        var router = new FakeCommandRouter();
        var resultado = await new FunctionEndpoint(router).Run(Request(), "no-es-guid", CancellationToken.None);
        resultado.Should().BeOfType<BadRequestObjectResult>();
        router.Invocado.Should().BeFalse();
    }

    [Fact]
    public async Task QuitarModalidadDeJornada_Retorna404_CuandoRouterLanzaRecursoNoEncontradoException()
    {
        var router = new FakeCommandRouter(new RecursoNoEncontradoException(
            QuitarModalidadDeJornadaCommandHandler.Mensajes.JornadaNoEncontrada));
        var resultado = await new FunctionEndpoint(router).Run(Request(), JornadaId.ToString(), CancellationToken.None);
        resultado.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task QuitarModalidadDeJornada_Retorna409ConElMensaje_CuandoRouterLanzaReglaDeNegocioDeclinada()
    {
        const string mensaje = "Ya existe una Jornada con estos limites y sin modalidad: 3f2b9c1e";
        var router = new FakeCommandRouter(new ReglaDeNegocioDeclinadaException(mensaje));
        var resultado = await new FunctionEndpoint(router).Run(Request(), JornadaId.ToString(), CancellationToken.None);
        resultado.Should().BeOfType<ConflictObjectResult>().Which.Value.Should().Be(mensaje);
    }
}
