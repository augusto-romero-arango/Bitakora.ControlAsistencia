using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearPlantillaTurnosFunction;
using Bitakora.ControlAsistencia.Programacion.CrearPlantillaTurnosFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Azure.Functions.Worker;

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
        var function = new FunctionEndpoint(new FakeRequestValidator(comando), new FakeCommandRouter());

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
        ((CreatedResult)result).Location.Should().Be($"/api/programacion/plantillas-turnos/{comando.PlantillaId}");
    }

    [Fact]
    public async Task CrearPlantillaTurnos_NuncaRetornaAcceptedResult_CuandoComandoEsValido()
    {
        var function = new FunctionEndpoint(new FakeRequestValidator(ComandoValido()), new FakeCommandRouter());

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().NotBeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task CrearPlantillaTurnos_Retorna409_CuandoPlantillaYaExiste()
    {
        var function = new FunctionEndpoint(
            new FakeRequestValidator(ComandoValido()),
            new FakeCommandRouter(excepcion: new RecursoYaExisteException(
                CrearPlantillaTurnosCommandHandler.Mensajes.PlantillaYaExiste)));

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task CrearPlantillaTurnos_Retorna404_CuandoLaJornadaNoExiste()
    {
        var function = new FunctionEndpoint(
            new FakeRequestValidator(ComandoValido()),
            new FakeCommandRouter(excepcion: new RecursoNoEncontradoException(
                CrearPlantillaTurnosCommandHandler.Mensajes.JornadaNoEncontrada)));

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CrearPlantillaTurnos_Retorna409_CuandoLaJornadaEstaDesactivada()
    {
        var function = new FunctionEndpoint(
            new FakeRequestValidator(ComandoValido()),
            new FakeCommandRouter(excepcion: new ReglaDeNegocioDeclinadaException(
                CrearPlantillaTurnosCommandHandler.Mensajes.JornadaDesactivada)));

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task CrearPlantillaTurnos_Retorna400_CuandoRequestEsInvalido()
    {
        var function = new FunctionEndpoint(
            new FakeRequestValidator(error: new BadRequestObjectResult("body invalido")),
            new FakeCommandRouter());

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CrearPlantillaTurnos_Retorna400ConMensajes_CuandoElFactoryRechazaLosDatos()
    {
        var errores = new ArgumentException[] { new("nombre vacio"), new("dias fuera de rango") };
        var function = new FunctionEndpoint(
            new FakeRequestValidator(ComandoValido()),
            new FakeCommandRouter(erroresAggregateException: errores));

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>().Which.Value
            .Should().BeAssignableTo<IEnumerable<string>>()
            .Which.Should().BeEquivalentTo(errores.Select(e => e.Message));
    }
}

internal sealed class FakeRequestValidator(CrearPlantillaTurnos? comando = null, IActionResult? error = null)
    : IRequestValidator
{
    public Task<(T? Comando, IActionResult? Error)> ValidarAsync<T>(HttpRequest req, CancellationToken ct) =>
        Task.FromResult<(T?, IActionResult?)>(error is null && comando is T resultado
            ? (resultado, null)
            : (default, error));
}

internal sealed class FakeCommandRouter(
    Exception? excepcion = null, ArgumentException[]? erroresAggregateException = null) : ICommandRouter
{
    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (excepcion is not null)
            throw excepcion;

        if (erroresAggregateException is not null)
            throw new AggregateException(erroresAggregateException);

        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
