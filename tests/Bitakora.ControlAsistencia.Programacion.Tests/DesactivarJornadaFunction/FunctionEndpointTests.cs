using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DesactivarJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.DesactivarJornadaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.Tests.DesactivarJornadaFunction;

public class FunctionEndpointTests
{
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000000872");

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    [Fact]
    public void DesactivarJornada_ExponeElVerboYLaRutaPactadosEnElIssue()
    {
        var trigger = typeof(FunctionEndpoint).GetMethod(nameof(FunctionEndpoint.Run))!
            .GetParameters().Select(p => p.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(t => t is not null)!;

        trigger.Methods.Should().Equal("post");
        trigger.Route.Should().Be("programacion/jornadas/{id}:desactivar");
    }

    [Fact]
    public async Task DesactivarJornada_Retorna204SinCuerpo_CuandoElComandoEsValido()
    {
        var router = new FakeCommandRouter();

        var resultado = await new FunctionEndpoint(router)
            .Run(FakeHttpRequest(), JornadaId.ToString(), CancellationToken.None);

        resultado.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        resultado.Should().NotBeAssignableTo<ObjectResult>();
        router.Comando.Should().Be(new DesactivarJornada(JornadaId));
    }

    [Fact]
    public async Task DesactivarJornada_Retorna204_CuandoLaJornadaYaEstabaInactiva()
    {
        var resultado = await new FunctionEndpoint(new FakeCommandRouter())
            .Run(FakeHttpRequest(), JornadaId.ToString(), CancellationToken.None);

        resultado.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        resultado.Should().NotBeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task DesactivarJornada_Retorna400_CuandoElIdNoEsGuid()
    {
        var router = new FakeCommandRouter();

        var resultado = await new FunctionEndpoint(router)
            .Run(FakeHttpRequest(), "no-es-guid", CancellationToken.None);

        resultado.Should().BeOfType<BadRequestObjectResult>();
        router.Comando.Should().BeNull();
    }

    [Fact]
    public async Task DesactivarJornada_Retorna404_CuandoLaJornadaNoExiste()
    {
        var router = new FakeCommandRouter(
            new RecursoNoEncontradoException(DesactivarJornadaCommandHandler.Mensajes.JornadaNoEncontrada));

        var resultado = await new FunctionEndpoint(router)
            .Run(FakeHttpRequest(), JornadaId.ToString(), CancellationToken.None);

        resultado.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DesactivarJornada_Retorna409_CuandoLaJornadaEstaEnUso()
    {
        var router = new FakeCommandRouter(
            new ReglaDeNegocioDeclinadaException(DesactivarJornadaCommandHandler.Mensajes.JornadaPredeterminada));

        var resultado = await new FunctionEndpoint(router)
            .Run(FakeHttpRequest(), JornadaId.ToString(), CancellationToken.None);

        resultado.Should().BeOfType<ConflictObjectResult>();
    }

    private sealed class FakeCommandRouter(Exception? excepcion = null) : ICommandRouter
    {
        public DesactivarJornada? Comando { get; private set; }

        public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
            where TCommand : class
        {
            Comando = command as DesactivarJornada;
            if (excepcion is not null) throw excepcion;
            return Task.CompletedTask;
        }

        public Task<TResult> InvokeAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default)
            where TCommand : class =>
            throw new NotImplementedException();
    }
}
