using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.AsignarJornadaFunction;
using Bitakora.ControlAsistencia.Colaboradores.AsignarJornadaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AsignarJornadaFunction;

public class FunctionEndpointTests
{
    private const string Id = "CC-79543210";
    private static readonly Guid JornadaId = Guid.Parse("12345678-1234-4123-8123-123456789abc");

    private static FunctionEndpoint CrearEndpoint(FakeJornadaRouter router, IActionResult? error = null) =>
        new(new FakeJornadaValidator(new AsignarJornadaBody(JornadaId), error), router);

    [Fact]
    public void AsignarJornada_DeclaraPutYLaRutaDeJornada()
    {
        var run = typeof(FunctionEndpoint).GetMethod(nameof(FunctionEndpoint.Run))!;
        var trigger = run.GetParameters()[0].GetCustomAttribute<HttpTriggerAttribute>()!;

        trigger.Methods.Should().ContainSingle().Which.Should().Be("put");
        trigger.Route.Should().Be("colaboradores/{id}/jornada");
        run.GetCustomAttribute<FunctionAttribute>()!.Name.Should().Be("AsignarJornada");
    }

    [Fact]
    public async Task AsignarJornada_Retorna204SinCuerpo_CuandoAsignaJornada()
    {
        var resultado = await CrearEndpoint(new FakeJornadaRouter()).Run(new DefaultHttpContext().Request, Id,
            TestContext.Current.CancellationToken);

        resultado.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        resultado.Should().NotBeAssignableTo<ObjectResult>();
    }

    [Fact]
    public async Task AsignarJornada_Retorna204SinCuerpo_CuandoLaJornadaYaEstaAsignada()
    {
        var resultado = await CrearEndpoint(new FakeJornadaRouter()).Run(new DefaultHttpContext().Request, Id,
            TestContext.Current.CancellationToken);

        resultado.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        resultado.Should().NotBeAssignableTo<ObjectResult>();
    }

    [Fact]
    public async Task AsignarJornada_ComponeComando_ConIdentificacionDeRutaYJornadaDelBody()
    {
        var router = new FakeJornadaRouter();

        await CrearEndpoint(router).Run(new DefaultHttpContext().Request, Id, TestContext.Current.CancellationToken);

        router.ComandoRecibido.Should().Be(new AsignarJornada("CC", "79543210", JornadaId));
    }

    [Fact]
    public async Task AsignarJornada_Retorna400_CuandoIdDeRutaEsInvalido()
    {
        var router = new FakeJornadaRouter();

        var resultado = await CrearEndpoint(router).Run(new DefaultHttpContext().Request, "CC79543210",
            TestContext.Current.CancellationToken);

        resultado.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        router.ComandoRecibido.Should().BeNull();
    }

    [Fact]
    public async Task AsignarJornada_Retorna400_CuandoBodyNoTieneJornadaId()
    {
        var router = new FakeJornadaRouter();
        var endpoint = CrearEndpoint(router, new BadRequestObjectResult("body invalido"));

        var resultado = await endpoint.Run(new DefaultHttpContext().Request, Id, TestContext.Current.CancellationToken);

        resultado.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        router.ComandoRecibido.Should().BeNull();
    }

    [Fact]
    public async Task AsignarJornada_Retorna404_CuandoColaboradorNoExiste()
    {
        var endpoint = CrearEndpoint(new FakeJornadaRouter(new RecursoNoEncontradoException(
            AsignarJornadaCommandHandler.Mensajes.ColaboradorNoEncontrado)));

        var resultado = await endpoint.Run(new DefaultHttpContext().Request, Id, TestContext.Current.CancellationToken);

        resultado.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task AsignarJornada_Retorna409_CuandoVinculacionTieneTerminacionRegistrada()
    {
        var endpoint = CrearEndpoint(new FakeJornadaRouter(new ReglaDeNegocioDeclinadaException(
            AsignarJornadaCommandHandler.Mensajes.VinculacionTerminada)));

        var resultado = await endpoint.Run(new DefaultHttpContext().Request, Id, TestContext.Current.CancellationToken);

        resultado.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }
}

internal class FakeJornadaValidator(AsignarJornadaBody body, IActionResult? error = null) : IRequestValidator
{
    public Task<(T? Comando, IActionResult? Error)> ValidarAsync<T>(HttpRequest req, CancellationToken ct) =>
        Task.FromResult<(T?, IActionResult?)>((error is null ? (T?)(object)body : default, error));
}

internal class FakeJornadaRouter(Exception? exception = null) : ICommandRouter
{
    public AsignarJornada? ComandoRecibido { get; private set; }

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default) where TCommand : class
    {
        ComandoRecibido = command as AsignarJornada;
        if (exception is not null) throw exception;
        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default)
        where TCommand : class => throw new NotImplementedException();
}
