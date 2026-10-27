using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.ModificarVentanaDeEntradaFunction;
using Bitakora.ControlAsistencia.Programacion.ModificarVentanaDeEntradaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.Tests.Soporte;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ModificarVentanaDeEntradaFunction;

public class FunctionEndpointTests
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000001106");

    private static ModificarVentanaDeEntradaBody Body() => new(new(3, 0));
    private static HttpRequest Request() => new DefaultHttpContext().Request;

    private static FunctionEndpoint Endpoint(FakeCommandRouter router,
        FakeRequestValidator<ModificarVentanaDeEntradaBody>? validator = null) =>
        new(validator ?? new FakeRequestValidator<ModificarVentanaDeEntradaBody>(Body()), router);

    [Fact]
    public void ModificarVentanaDeEntrada_ExponePutEnRutaVentanaEntradaDelTurno()
    {
        var metodo = typeof(FunctionEndpoint).GetMethod(nameof(FunctionEndpoint.Run))!;
        metodo.GetCustomAttribute<FunctionAttribute>()!.Name.Should().Be("ModificarVentanaDeEntrada");
        var trigger = metodo.GetParameters().Select(p => p.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(a => a is not null)!;
        trigger.Methods.Should().Equal("put");
        trigger.Route.Should().Be("programacion/turnos/{id}/ventana-entrada");
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_Retorna204_CuandoComandoEsValido()
    {
        var router = new FakeCommandRouter();
        var resultado = await Endpoint(router).Run(Request(), TurnoId.ToString(), CancellationToken.None);
        resultado.Should().BeOfType<NoContentResult>();
        resultado.Should().NotBeOfType<AcceptedResult>();
        router.Invocado.Should().BeTrue();
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_Retorna204_CuandoElEstadoYaEstabaAlcanzado()
    {
        var router = new FakeCommandRouter();
        var resultado = await Endpoint(router).Run(Request(), TurnoId.ToString(), CancellationToken.None);
        resultado.Should().BeOfType<NoContentResult>();
        resultado.Should().NotBeOfType<ConflictObjectResult>();
        resultado.Should().NotBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_Retorna400SinInvocarElRouter_CuandoElIdNoEsGuid()
    {
        var router = new FakeCommandRouter();
        var resultado = await Endpoint(router).Run(Request(), "no-es-guid", CancellationToken.None);
        resultado.Should().BeOfType<BadRequestObjectResult>();
        router.Invocado.Should().BeFalse();
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_Retorna400_CuandoValidadorRechazaElBody()
    {
        var router = new FakeCommandRouter();
        var validator = new FakeRequestValidator<ModificarVentanaDeEntradaBody>(
            error: new BadRequestObjectResult("El body es invalido"));
        var resultado = await Endpoint(router, validator).Run(Request(), TurnoId.ToString(), CancellationToken.None);
        resultado.Should().BeOfType<BadRequestObjectResult>();
        router.Invocado.Should().BeFalse();
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_Retorna400ConLosMensajes_CuandoRouterLanzaAggregateException()
    {
        var router = new FakeCommandRouter(new AggregateException(
            new ArgumentException(HorasYMinutos.Mensajes.MinutosFueraDeRango)));
        var resultado = await Endpoint(router).Run(Request(), TurnoId.ToString(), CancellationToken.None);
        var mensajes = resultado.Should().BeOfType<BadRequestObjectResult>().Which.Value;
        mensajes.Should().BeAssignableTo<IEnumerable<string>>().Which.Should().BeEquivalentTo(
            new[] { HorasYMinutos.Mensajes.MinutosFueraDeRango });
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_Retorna404_CuandoRouterLanzaRecursoNoEncontradoException()
    {
        var router = new FakeCommandRouter(new RecursoNoEncontradoException(
            ModificarVentanaDeEntradaCommandHandler.Mensajes.TurnoNoEncontrado));
        var resultado = await Endpoint(router).Run(Request(), TurnoId.ToString(), CancellationToken.None);
        resultado.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_Retorna409ConElMensaje_CuandoRouterLanzaReglaDeNegocioDeclinada()
    {
        var router = new FakeCommandRouter(new ReglaDeNegocioDeclinadaException(
            ModificarVentanaDeEntradaCommandHandler.Mensajes.TurnoRetirado));
        var resultado = await Endpoint(router).Run(Request(), TurnoId.ToString(), CancellationToken.None);
        resultado.Should().BeOfType<ConflictObjectResult>().Which.Value.Should()
            .Be(ModificarVentanaDeEntradaCommandHandler.Mensajes.TurnoRetirado);
    }
}
