using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.ModificarLimitesJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.ModificarLimitesJornadaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.Tests.AsignarTurnoADiaDePlantillaSemanalFunction;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ModificarLimitesJornadaFunction;

public class FunctionEndpointTests
{
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000000856");

    private static ModificarLimitesJornadaBody Body() => new(new(44, 0), new(8, 0), new(0, 0), 1);
    private static HttpRequest Request() => new DefaultHttpContext().Request;

    private static FunctionEndpoint Endpoint(FakeCommandRouter router,
        FakeRequestValidator<ModificarLimitesJornadaBody>? validator = null) =>
        new(validator ?? new FakeRequestValidator<ModificarLimitesJornadaBody>(Body()), router);

    [Fact]
    public void ModificarLimitesJornada_ExponePutEnRutaLimitesDeLaJornada()
    {
        var metodo = typeof(FunctionEndpoint).GetMethod(nameof(FunctionEndpoint.Run))!;
        metodo.GetCustomAttribute<FunctionAttribute>()!.Name.Should().Be("ModificarLimitesJornada");
        var trigger = metodo.GetParameters().Select(p => p.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(a => a is not null)!;
        trigger.Methods.Should().Equal("put");
        trigger.Route.Should().Be("programacion/jornadas/{id}/limites");
    }

    [Fact]
    public async Task ModificarLimitesJornada_Retorna204_CuandoComandoEsValido()
    {
        var router = new FakeCommandRouter();
        var resultado = await Endpoint(router).Run(Request(), JornadaId.ToString(), CancellationToken.None);
        resultado.Should().BeOfType<NoContentResult>();
        resultado.Should().NotBeOfType<AcceptedResult>();
        router.Invocado.Should().BeTrue();
    }

    [Fact]
    public async Task ModificarLimitesJornada_Retorna400SinInvocarElRouter_CuandoElIdNoEsGuid()
    {
        var router = new FakeCommandRouter();
        var resultado = await Endpoint(router).Run(Request(), "no-es-guid", CancellationToken.None);
        resultado.Should().BeOfType<BadRequestObjectResult>();
        router.Invocado.Should().BeFalse();
    }

    [Fact]
    public async Task ModificarLimitesJornada_Retorna400_CuandoValidadorRechazaElBody()
    {
        var router = new FakeCommandRouter();
        var validator = new FakeRequestValidator<ModificarLimitesJornadaBody>(
            error: new BadRequestObjectResult("El body es invalido"));
        var resultado = await Endpoint(router, validator).Run(Request(), JornadaId.ToString(), CancellationToken.None);
        resultado.Should().BeOfType<BadRequestObjectResult>();
        router.Invocado.Should().BeFalse();
    }

    [Fact]
    public async Task ModificarLimitesJornada_Retorna400ConTodosLosMensajes_CuandoRouterLanzaAggregateException()
    {
        var router = new FakeCommandRouter(new AggregateException(
            new ArgumentException(HorasYMinutos.Mensajes.MinutosFueraDeRango),
            new ArgumentException(LimitesJornada.Mensajes.DescansosFueraDeRango)));
        var resultado = await Endpoint(router).Run(Request(), JornadaId.ToString(), CancellationToken.None);
        var mensajes = resultado.Should().BeOfType<BadRequestObjectResult>().Which.Value;
        mensajes.Should().BeAssignableTo<IEnumerable<string>>().Which.Should().BeEquivalentTo(new[]
        {
            HorasYMinutos.Mensajes.MinutosFueraDeRango,
            LimitesJornada.Mensajes.DescansosFueraDeRango
        });
    }

    [Fact]
    public async Task ModificarLimitesJornada_Retorna404_CuandoRouterLanzaRecursoNoEncontradoException()
    {
        var router = new FakeCommandRouter(new RecursoNoEncontradoException(
            ModificarLimitesJornadaCommandHandler.Mensajes.JornadaNoEncontrada));
        var resultado = await Endpoint(router).Run(Request(), JornadaId.ToString(), CancellationToken.None);
        resultado.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ModificarLimitesJornada_Retorna409ConElMensaje_CuandoRouterLanzaReglaDeNegocioDeclinada()
    {
        const string mensaje = "Ya existe una Jornada con estos limites: 3f2b9c1e";
        var router = new FakeCommandRouter(new ReglaDeNegocioDeclinadaException(mensaje));
        var resultado = await Endpoint(router).Run(Request(), JornadaId.ToString(), CancellationToken.None);
        resultado.Should().BeOfType<ConflictObjectResult>().Which.Value.Should().Be(mensaje);
    }
}
