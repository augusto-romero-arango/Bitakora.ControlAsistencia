using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.IniciarVinculacionFunction;
using Bitakora.ControlAsistencia.Colaboradores.IniciarVinculacionFunction.CommandHandler;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.IniciarVinculacionFunction;

public class FunctionEndpointTests
{
    private const string IdValido = "CC-79543210";

    private static IniciarVinculacionBody BodyValido() => new(
        CodigoColaborador: "COL-002",
        FechaInicio: new DateOnly(2026, 6, 2));

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    // Location usa el {id} tal como llego en la ruta, sin re-parsear.
    [Fact]
    public async Task IniciarVinculacion_Retorna201ConLocation_CuandoIdDeRutaYBodySonValidos()
    {
        var validator = new FakeIniciarVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeIniciarVinculacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
        result.As<CreatedResult>().Location.Should().Be($"/api/colaboradores/fichas/{IdValido}");
    }

    [Fact]
    public async Task IniciarVinculacion_ComponeElComando_DesdeIdDeRutaYBody()
    {
        var validator = new FakeIniciarVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeIniciarVinculacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        router.ComandoRecibido.Should().Be(new IniciarVinculacion(
            TipoIdentificacion: "CC",
            NumeroIdentificacion: "79543210",
            CodigoColaborador: "COL-002",
            FechaInicio: new DateOnly(2026, 6, 2)));
    }

    // CodigoSede es opcional y se forwardea tal cual (null si el body no lo trae).
    [Fact]
    public async Task IniciarVinculacion_ComponeElComando_ConCodigoSedeDesdeElBody()
    {
        var validator = new FakeIniciarVinculacionBodyRequestValidator(
            BodyValido() with { CodigoSede = "BOG" });
        var router = new FakeIniciarVinculacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        router.ComandoRecibido.Should().Be(new IniciarVinculacion(
            TipoIdentificacion: "CC",
            NumeroIdentificacion: "79543210",
            CodigoColaborador: "COL-002",
            FechaInicio: new DateOnly(2026, 6, 2),
            CodigoSede: "BOG"));
    }

    [Fact]
    public async Task IniciarVinculacion_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var validator = new FakeIniciarVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeIniciarVinculacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "CC79543210", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con un id invalido");
    }

    [Fact]
    public async Task IniciarVinculacion_Retorna400_CuandoElTipoDeLaIdentificacionNoEstaEnLaListaCerrada()
    {
        var validator = new FakeIniciarVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeIniciarVinculacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "XX-79543210", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con un id invalido");
    }

    [Fact]
    public async Task IniciarVinculacion_Retorna400_CuandoElNumeroDeLaIdentificacionQuedaVacio()
    {
        var validator = new FakeIniciarVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeIniciarVinculacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "CC-", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con un id invalido");
    }

    [Fact]
    public async Task IniciarVinculacion_Retorna400_CuandoElBodyEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeIniciarVinculacionBodyRequestValidator(error: errorDeValidacion);
        var router = new FakeIniciarVinculacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task IniciarVinculacion_Retorna409_CuandoLaVinculacionVigenteEstaAbierta()
    {
        var validator = new FakeIniciarVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeIniciarVinculacionCommandRouter(
            lanzar: new ReglaDeNegocioDeclinadaException(
                IniciarVinculacionCommandHandler.Mensajes.VinculacionAbierta));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task IniciarVinculacion_Retorna404_CuandoColaboradorNoExiste()
    {
        var validator = new FakeIniciarVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeIniciarVinculacionCommandRouter(
            lanzar: new RecursoNoEncontradoException(
                IniciarVinculacionCommandHandler.Mensajes.ColaboradorNoEncontrado));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task IniciarVinculacion_PropagaInvalidOperationException_CuandoFallaLaInfraestructura()
    {
        var function = new FunctionEndpoint(
            new FakeIniciarVinculacionBodyRequestValidator(BodyValido()),
            new FakeIniciarVinculacionCommandRouter(new InvalidOperationException()));

        var act = async () => await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>();
    }
}

internal class FakeIniciarVinculacionBodyRequestValidator : IRequestValidator
{
    private readonly IniciarVinculacionBody? _body;
    private readonly IActionResult? _error;

    public FakeIniciarVinculacionBodyRequestValidator(
        IniciarVinculacionBody? body = null,
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

internal class FakeIniciarVinculacionCommandRouter : ICommandRouter
{
    private readonly Exception? _excepcion;

    public IniciarVinculacion? ComandoRecibido { get; private set; }

    public FakeIniciarVinculacionCommandRouter(Exception? lanzar = null) =>
        _excepcion = lanzar;

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (command is IniciarVinculacion iniciarVinculacion)
            ComandoRecibido = iniciarVinculacion;

        if (_excepcion is not null)
            throw _excepcion;

        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(
        TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
