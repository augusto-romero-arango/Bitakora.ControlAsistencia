// {codigo} viaja intacto al comando: la comparacion contra el codigo vigente vive en el aggregate.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction;
using Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.TerminarVinculacionFunction;

public class FunctionEndpointTests
{
    private const string IdValido = "CC-79543210";
    private const string CodigoValido = "COL-001";

    private static TerminarVinculacionBody BodyValido() => new(FechaEfectiva: new DateOnly(2026, 6, 1));

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    [Fact]
    public async Task TerminarVinculacion_Retorna204SinCuerpo_CuandoIdDeRutaCodigoYBodySonValidos()
    {
        var validator = new FakeTerminarVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeTerminarVinculacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CodigoValido, CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        result.Should().NotBeAssignableTo<ObjectResult>();
    }

    [Fact]
    public async Task TerminarVinculacion_ComponeElComando_DesdeIdDeRutaCodigoYBody()
    {
        var validator = new FakeTerminarVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeTerminarVinculacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        await function.Run(FakeHttpRequest(), IdValido, CodigoValido, CancellationToken.None);

        router.ComandoRecibido.Should().Be(new TerminarVinculacion(
            TipoIdentificacion: "CC",
            NumeroIdentificacion: "79543210",
            Codigo: CodigoValido,
            FechaEfectiva: new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public async Task TerminarVinculacion_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var validator = new FakeTerminarVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeTerminarVinculacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "CC79543210", CodigoValido, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con un id invalido");
    }

    [Fact]
    public async Task TerminarVinculacion_Retorna400_CuandoElTipoDeLaIdentificacionNoEstaEnLaListaCerrada()
    {
        var validator = new FakeTerminarVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeTerminarVinculacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "XX-79543210", CodigoValido, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con un id invalido");
    }

    [Fact]
    public async Task TerminarVinculacion_Retorna400_CuandoElNumeroDeLaIdentificacionQuedaVacio()
    {
        var validator = new FakeTerminarVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeTerminarVinculacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "CC-", CodigoValido, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con un id invalido");
    }

    [Fact]
    public async Task TerminarVinculacion_Retorna400_CuandoElBodyEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeTerminarVinculacionBodyRequestValidator(error: errorDeValidacion);
        var router = new FakeTerminarVinculacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CodigoValido, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TerminarVinculacion_Retorna409_CuandoLaVinculacionYaTieneTerminacionRegistrada()
    {
        var validator = new FakeTerminarVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeTerminarVinculacionCommandRouter(
            lanzar: new ReglaDeNegocioDeclinadaException(
                TerminarVinculacionCommandHandler.Mensajes.VinculacionYaTerminada));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CodigoValido, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task TerminarVinculacion_Retorna404_CuandoColaboradorNoExiste()
    {
        var validator = new FakeTerminarVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeTerminarVinculacionCommandRouter(
            lanzar: new RecursoNoEncontradoException(
                TerminarVinculacionCommandHandler.Mensajes.ColaboradorNoEncontrado));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CodigoValido, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task TerminarVinculacion_PropagaInvalidOperationException_CuandoFallaLaInfraestructura()
    {
        var function = new FunctionEndpoint(
            new FakeTerminarVinculacionBodyRequestValidator(BodyValido()),
            new FakeTerminarVinculacionCommandRouter(new InvalidOperationException()));

        var act = async () => await function.Run(FakeHttpRequest(), IdValido, CodigoValido, CancellationToken.None);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>();
    }
}

internal class FakeTerminarVinculacionBodyRequestValidator : IRequestValidator
{
    private readonly TerminarVinculacionBody? _body;
    private readonly IActionResult? _error;

    public FakeTerminarVinculacionBodyRequestValidator(
        TerminarVinculacionBody? body = null,
        IActionResult? error = null)
    {
        _body = body;
        _error = error;
    }

    public Task<(T? Comando, IActionResult? Error)> ValidarAsync<T>(
        HttpRequest req, CancellationToken ct)
    {
        if (_error is not null)
            return Task.FromResult<(T?, IActionResult?)>((default, _error));

        if (_body is T resultado)
            return Task.FromResult<(T?, IActionResult?)>((resultado, null));

        return Task.FromResult<(T?, IActionResult?)>((default, null));
    }
}

internal class FakeTerminarVinculacionCommandRouter : ICommandRouter
{
    private readonly Exception? _excepcion;

    public TerminarVinculacion? ComandoRecibido { get; private set; }

    public FakeTerminarVinculacionCommandRouter(Exception? lanzar = null) =>
        _excepcion = lanzar;

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (command is TerminarVinculacion terminarVinculacion)
            ComandoRecibido = terminarVinculacion;

        if (_excepcion is not null)
            throw _excepcion;

        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(
        TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
