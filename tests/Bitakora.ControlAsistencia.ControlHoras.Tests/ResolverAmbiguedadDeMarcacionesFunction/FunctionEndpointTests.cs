using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.ControlHoras.ResolverAmbiguedadDeMarcacionesFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ResolverAmbiguedadDeMarcacionesFunction;

public class FunctionEndpointTests
{
    private const string CodigoColaboradorValido = "EMP-001";
    private const string FechaValida = "2026-10-21";

    private static readonly DecisionDeMarcacion Decision = new(new DateTime(2026, 10, 21, 4, 30, 0), new DateOnly(2026, 10, 20));

    private static ResolverAmbiguedadDeMarcacionesBody BodyValido() => new([Decision]);

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    [Fact]
    public async Task ResolverAmbiguedadDeMarcaciones_Retorna204SinCuerpo_CuandoFechaYBodySonValidos()
    {
        var function = new FunctionEndpoint(
            new FakeResolverBodyRequestValidator(BodyValido()), new FakeResolverCommandRouter());

        var result = await function.Run(
            FakeHttpRequest(), CodigoColaboradorValido, FechaValida, CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        result.Should().NotBeAssignableTo<ObjectResult>();
    }

    [Fact]
    public async Task ResolverAmbiguedadDeMarcaciones_ComponeElComando_DesdeRutaYBody()
    {
        var router = new FakeResolverCommandRouter();
        var function = new FunctionEndpoint(new FakeResolverBodyRequestValidator(BodyValido()), router);

        await function.Run(FakeHttpRequest(), CodigoColaboradorValido, FechaValida, CancellationToken.None);

        router.ComandoRecibido.Should().BeEquivalentTo(new ResolverAmbiguedadDeMarcaciones(
            CodigoColaboradorValido, new DateOnly(2026, 10, 21), [Decision]));
    }

    [Fact]
    public async Task ResolverAmbiguedadDeMarcaciones_Retorna400_CuandoLaFechaNoTieneElFormatoEsperado()
    {
        var router = new FakeResolverCommandRouter();
        var function = new FunctionEndpoint(new FakeResolverBodyRequestValidator(BodyValido()), router);

        var result = await function.Run(
            FakeHttpRequest(), CodigoColaboradorValido, "21-10-2026", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull();
    }

    [Fact]
    public async Task ResolverAmbiguedadDeMarcaciones_Retorna400_CuandoElBodyEsInvalido()
    {
        var router = new FakeResolverCommandRouter();
        var validator = new FakeResolverBodyRequestValidator(
            error: new BadRequestObjectResult("El body es invalido o esta malformado"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(
            FakeHttpRequest(), CodigoColaboradorValido, FechaValida, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull();
    }

    [Fact]
    public async Task ResolverAmbiguedadDeMarcaciones_Retorna409_CuandoLaReglaDeNegocioDeclinaElComando()
    {
        var router = new FakeResolverCommandRouter(new ReglaDeNegocioDeclinadaException("reparto incompleto"));
        var function = new FunctionEndpoint(new FakeResolverBodyRequestValidator(BodyValido()), router);

        var result = await function.Run(
            FakeHttpRequest(), CodigoColaboradorValido, FechaValida, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task ResolverAmbiguedadDeMarcaciones_Retorna404_CuandoElDiaNoTieneDepuracion()
    {
        var router = new FakeResolverCommandRouter(new RecursoNoEncontradoException("sin depuracion"));
        var function = new FunctionEndpoint(new FakeResolverBodyRequestValidator(BodyValido()), router);

        var result = await function.Run(
            FakeHttpRequest(), CodigoColaboradorValido, FechaValida, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ResolverAmbiguedadDeMarcaciones_PropagaInvalidOperationException_CuandoElRouterLaLanza()
    {
        var router = new FakeResolverCommandRouter(new InvalidOperationException("fallo de infraestructura"));
        var function = new FunctionEndpoint(new FakeResolverBodyRequestValidator(BodyValido()), router);

        var act = async () => await function.Run(
            FakeHttpRequest(), CodigoColaboradorValido, FechaValida, CancellationToken.None);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>();
    }
}

internal class FakeResolverBodyRequestValidator : IRequestValidator
{
    private readonly ResolverAmbiguedadDeMarcacionesBody? _body;
    private readonly IActionResult? _error;

    public FakeResolverBodyRequestValidator(ResolverAmbiguedadDeMarcacionesBody? body = null, IActionResult? error = null)
    {
        _body = body;
        _error = error;
    }

    public Task<(T? Comando, IActionResult? Error)> ValidarAsync<T>(HttpRequest req, CancellationToken ct)
    {
        if (_error is not null)
            return Task.FromResult<(T?, IActionResult?)>((default, _error));

        if (_body is T resultado)
            return Task.FromResult<(T?, IActionResult?)>((resultado, null));

        return Task.FromResult<(T?, IActionResult?)>((default, null));
    }
}

internal class FakeResolverCommandRouter : ICommandRouter
{
    private readonly Exception? _excepcion;

    public ResolverAmbiguedadDeMarcaciones? ComandoRecibido { get; private set; }

    public FakeResolverCommandRouter(Exception? lanzar = null) => _excepcion = lanzar;

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (command is ResolverAmbiguedadDeMarcaciones comando)
            ComandoRecibido = comando;

        if (_excepcion is not null)
            throw _excepcion;

        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
