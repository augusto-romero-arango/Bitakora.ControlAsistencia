using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.ActivarJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.ActivarJornadaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ActivarJornadaFunction;

public class FunctionEndpointTests
{
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000000940");

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    [Fact]
    public void ActivarJornada_ExponeElVerboYLaRutaPactadosEnElIssue()
    {
        var trigger = typeof(FunctionEndpoint).GetMethod(nameof(FunctionEndpoint.Run))!
            .GetParameters().Select(p => p.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(t => t is not null)!;

        trigger.Methods.Should().Equal("post");
        trigger.Route.Should().Be("programacion/jornadas/{id}:activar");
    }

    [Fact]
    public async Task ActivarJornada_Retorna204SinCuerpo_CuandoElComandoEsValido()
    {
        var router = new FakeCommandRouter();

        var resultado = await new FunctionEndpoint(router)
            .Run(FakeHttpRequest(), JornadaId.ToString(), CancellationToken.None);

        resultado.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        resultado.Should().NotBeAssignableTo<ObjectResult>();
        router.Comando.Should().Be(new ActivarJornada(JornadaId));
    }

    [Fact]
    public async Task ActivarJornada_Retorna204_CuandoLaJornadaYaEstabaActiva()
    {
        var resultado = await new FunctionEndpoint(new FakeCommandRouter())
            .Run(FakeHttpRequest(), JornadaId.ToString(), CancellationToken.None);

        resultado.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        resultado.Should().NotBeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task ActivarJornada_Retorna400_CuandoElIdNoEsGuid()
    {
        var router = new FakeCommandRouter();

        var resultado = await new FunctionEndpoint(router)
            .Run(FakeHttpRequest(), "no-es-guid", CancellationToken.None);

        resultado.Should().BeOfType<BadRequestObjectResult>();
        router.Comando.Should().BeNull();
    }

    [Fact]
    public async Task ActivarJornada_Retorna404_CuandoLaJornadaNoExiste()
    {
        var router = new FakeCommandRouter(
            new RecursoNoEncontradoException(ActivarJornadaCommandHandler.Mensajes.JornadaNoEncontrada));

        var resultado = await new FunctionEndpoint(router)
            .Run(FakeHttpRequest(), JornadaId.ToString(), CancellationToken.None);

        resultado.Should().BeOfType<NotFoundObjectResult>();
    }

    private sealed class FakeCommandRouter(Exception? excepcion = null) : ICommandRouter
    {
        public ActivarJornada? Comando { get; private set; }

        public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
            where TCommand : class
        {
            Comando = command as ActivarJornada;
            if (excepcion is not null) throw excepcion;
            return Task.CompletedTask;
        }

        public Task<TResult> InvokeAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default)
            where TCommand : class =>
            throw new NotImplementedException();
    }
}
