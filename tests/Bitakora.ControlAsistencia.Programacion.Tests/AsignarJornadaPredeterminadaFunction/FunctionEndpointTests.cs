using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AsignarJornadaPredeterminadaFunction;
using Bitakora.ControlAsistencia.Programacion.AsignarJornadaPredeterminadaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.Tests.AsignarTurnoADiaDePlantillaSemanalFunction;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AsignarJornadaPredeterminadaFunction;

public class FunctionEndpointTests
{
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000000858");

    private static HttpRequest Request() => new DefaultHttpContext().Request;

    private static FunctionEndpoint Endpoint(FakeCommandRouter router,
        FakeRequestValidator<AsignarJornadaPredeterminadaBody>? validator = null) =>
        new(validator ?? new FakeRequestValidator<AsignarJornadaPredeterminadaBody>(
            new AsignarJornadaPredeterminadaBody(JornadaId)), router);

    [Fact]
    public void AsignarJornadaPredeterminada_ExponePutEnRutaDeJornadaPredeterminada()
    {
        var metodo = typeof(FunctionEndpoint).GetMethod(nameof(FunctionEndpoint.Run))!;
        metodo.GetCustomAttribute<FunctionAttribute>()!.Name.Should().Be("AsignarJornadaPredeterminada");
        var trigger = metodo.GetParameters().Select(p => p.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(a => a is not null)!;
        trigger.Methods.Should().Equal("put");
        trigger.Route.Should().Be("programacion/preferencias/jornada-predeterminada");
    }

    [Fact]
    public async Task AsignarJornadaPredeterminada_Retorna204_CuandoComandoEsValido()
    {
        var router = new FakeCommandRouter();
        var resultado = await Endpoint(router).Run(Request(), CancellationToken.None);
        resultado.Should().BeOfType<NoContentResult>();
        resultado.Should().NotBeOfType<AcceptedResult>();
        router.Invocado.Should().BeTrue();
    }

    [Fact]
    public async Task AsignarJornadaPredeterminada_Retorna400SinInvocarElRouter_CuandoValidadorRechazaElBody()
    {
        var router = new FakeCommandRouter();
        var validator = new FakeRequestValidator<AsignarJornadaPredeterminadaBody>(
            error: new BadRequestObjectResult("El body es invalido"));
        var resultado = await Endpoint(router, validator).Run(Request(), CancellationToken.None);
        resultado.Should().BeOfType<BadRequestObjectResult>();
        router.Invocado.Should().BeFalse();
    }

    [Fact]
    public async Task AsignarJornadaPredeterminada_Retorna404_CuandoRouterLanzaRecursoNoEncontradoException()
    {
        var router = new FakeCommandRouter(new RecursoNoEncontradoException(
            AsignarJornadaPredeterminadaCommandHandler.Mensajes.JornadaNoEncontrada));
        var resultado = await Endpoint(router).Run(Request(), CancellationToken.None);
        resultado.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AsignarJornadaPredeterminada_Retorna204_CuandoLaJornadaYaEsLaPredeterminada()
    {
        var router = new FakeCommandRouter();
        var resultado = await Endpoint(router).Run(Request(), CancellationToken.None);
        resultado.Should().BeOfType<NoContentResult>();
        router.Invocado.Should().BeTrue();
    }
}
