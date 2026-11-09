using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AsignarModalidadAJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.AsignarModalidadAJornadaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.Tests.Soporte;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AsignarModalidadAJornadaFunction;

public class FunctionEndpointTests
{
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000001436");

    private static AsignarModalidadAJornadaBody Body() => new("Ordinaria");
    private static HttpRequest Request() => new DefaultHttpContext().Request;

    private static FunctionEndpoint Endpoint(FakeCommandRouter router,
        FakeRequestValidator<AsignarModalidadAJornadaBody>? validator = null) =>
        new(validator ?? new FakeRequestValidator<AsignarModalidadAJornadaBody>(Body()), router);

    [Fact]
    public void AsignarModalidadAJornada_ExponePutEnRutaModalidadDeLaJornada()
    {
        var metodo = typeof(FunctionEndpoint).GetMethod(nameof(FunctionEndpoint.Run))!;
        metodo.GetCustomAttribute<FunctionAttribute>()!.Name.Should().Be("AsignarModalidadAJornada");
        var trigger = metodo.GetParameters().Select(p => p.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(a => a is not null)!;
        trigger.Methods.Should().Equal("put");
        trigger.Route.Should().Be("programacion/jornadas/{id}/modalidad");
    }

    [Fact]
    public async Task AsignarModalidadAJornada_Retorna204SinCuerpo_CuandoElComandoEsValido()
    {
        var router = new FakeCommandRouter();
        var resultado = await Endpoint(router).Run(Request(), JornadaId.ToString(), CancellationToken.None);
        resultado.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        resultado.Should().NotBeAssignableTo<ObjectResult>();
        router.Invocado.Should().BeTrue();
    }

    [Fact]
    public async Task AsignarModalidadAJornada_Retorna204_CuandoLaJornadaYaTieneEsaModalidad()
    {
        var router = new FakeCommandRouter();
        var resultado = await Endpoint(router).Run(Request(), JornadaId.ToString(), CancellationToken.None);
        resultado.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
    }

    [Fact]
    public async Task AsignarModalidadAJornada_Retorna400SinInvocarElRouter_CuandoElIdNoEsGuid()
    {
        var router = new FakeCommandRouter();
        var resultado = await Endpoint(router).Run(Request(), "no-es-guid", CancellationToken.None);
        resultado.Should().BeOfType<BadRequestObjectResult>();
        router.Invocado.Should().BeFalse();
    }

    [Fact]
    public async Task AsignarModalidadAJornada_Retorna400_CuandoValidadorRechazaElBody()
    {
        var router = new FakeCommandRouter();
        var validator = new FakeRequestValidator<AsignarModalidadAJornadaBody>(
            error: new BadRequestObjectResult("El body es invalido"));
        var resultado = await Endpoint(router, validator).Run(Request(), JornadaId.ToString(), CancellationToken.None);
        resultado.Should().BeOfType<BadRequestObjectResult>();
        router.Invocado.Should().BeFalse();
    }

    [Fact]
    public async Task AsignarModalidadAJornada_Retorna404_CuandoRouterLanzaRecursoNoEncontradoException()
    {
        var router = new FakeCommandRouter(new RecursoNoEncontradoException(
            AsignarModalidadAJornadaCommandHandler.Mensajes.JornadaNoEncontrada));
        var resultado = await Endpoint(router).Run(Request(), JornadaId.ToString(), CancellationToken.None);
        resultado.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AsignarModalidadAJornada_Retorna409ConElMensaje_CuandoRouterLanzaReglaDeNegocioDeclinada()
    {
        const string mensaje = "Ya existe una Jornada con estos limites y esta modalidad: 3f2b9c1e";
        var router = new FakeCommandRouter(new ReglaDeNegocioDeclinadaException(mensaje));
        var resultado = await Endpoint(router).Run(Request(), JornadaId.ToString(), CancellationToken.None);
        resultado.Should().BeOfType<ConflictObjectResult>().Which.Value.Should().Be(mensaje);
    }
}
