// {codigo} viaja intacto al comando: la comparacion contra el codigo vigente vive en el aggregate.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction;
using Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction.CommandHandler;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.CorregirFechaInicioVinculacionFunction;

public class FunctionEndpointTests
{
    private const string IdValido = "CC-79543210";
    private const string CodigoValido = "COL-001";

    private static CorregirFechaInicioVinculacionBody BodyValido() =>
        new(FechaCorregida: new DateOnly(2026, 1, 10));

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    [Fact]
    public async Task CorregirFechaInicioVinculacion_Retorna204SinCuerpo_CuandoIdDeRutaCodigoYBodySonValidos()
    {
        var validator = new FakeCorregirFechaInicioVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeCorregirFechaInicioVinculacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CodigoValido, CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        result.Should().NotBeAssignableTo<ObjectResult>();
    }

    [Fact]
    public async Task CorregirFechaInicioVinculacion_ComponeElComando_DesdeIdDeRutaCodigoYBody()
    {
        var validator = new FakeCorregirFechaInicioVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeCorregirFechaInicioVinculacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        await function.Run(FakeHttpRequest(), IdValido, CodigoValido, CancellationToken.None);

        router.ComandoRecibido.Should().Be(new CorregirFechaInicioVinculacion(
            TipoIdentificacion: "CC",
            NumeroIdentificacion: "79543210",
            Codigo: CodigoValido,
            FechaCorregida: new DateOnly(2026, 1, 10)));
    }

    [Fact]
    public async Task CorregirFechaInicioVinculacion_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var validator = new FakeCorregirFechaInicioVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeCorregirFechaInicioVinculacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "CC79543210", CodigoValido, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con un id invalido");
    }

    [Fact]
    public async Task CorregirFechaInicioVinculacion_Retorna400_CuandoElTipoDeLaIdentificacionNoEstaEnLaListaCerrada()
    {
        var validator = new FakeCorregirFechaInicioVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeCorregirFechaInicioVinculacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "XX-79543210", CodigoValido, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con un id invalido");
    }

    [Fact]
    public async Task CorregirFechaInicioVinculacion_Retorna400_CuandoElNumeroDeLaIdentificacionQuedaVacio()
    {
        var validator = new FakeCorregirFechaInicioVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeCorregirFechaInicioVinculacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "CC-", CodigoValido, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con un id invalido");
    }

    [Fact]
    public async Task CorregirFechaInicioVinculacion_Retorna400_CuandoElBodyEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeCorregirFechaInicioVinculacionBodyRequestValidator(error: errorDeValidacion);
        var router = new FakeCorregirFechaInicioVinculacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CodigoValido, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CorregirFechaInicioVinculacion_Retorna409_CuandoLaFechaCorregidaVulneraUnaRegla()
    {
        var validator = new FakeCorregirFechaInicioVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeCorregirFechaInicioVinculacionCommandRouter(
            lanzar: new ReglaDeNegocioDeclinadaException(
                CorregirFechaInicioVinculacionCommandHandler.Mensajes.FechaPosteriorATerminacionPropia));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CodigoValido, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task CorregirFechaInicioVinculacion_Retorna404_CuandoColaboradorNoExiste()
    {
        var validator = new FakeCorregirFechaInicioVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeCorregirFechaInicioVinculacionCommandRouter(
            lanzar: new RecursoNoEncontradoException(
                CorregirFechaInicioVinculacionCommandHandler.Mensajes.ColaboradorNoEncontrado));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CodigoValido, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CorregirFechaInicioVinculacion_PropagaInvalidOperationException_CuandoFallaLaInfraestructura()
    {
        var function = new FunctionEndpoint(
            new FakeCorregirFechaInicioVinculacionBodyRequestValidator(BodyValido()),
            new FakeCorregirFechaInicioVinculacionCommandRouter(new InvalidOperationException()));

        var act = async () => await function.Run(FakeHttpRequest(), IdValido, CodigoValido, CancellationToken.None);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>();
    }
}

internal class FakeCorregirFechaInicioVinculacionBodyRequestValidator : IRequestValidator
{
    private readonly CorregirFechaInicioVinculacionBody? _body;
    private readonly IActionResult? _error;

    public FakeCorregirFechaInicioVinculacionBodyRequestValidator(
        CorregirFechaInicioVinculacionBody? body = null,
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

internal class FakeCorregirFechaInicioVinculacionCommandRouter : ICommandRouter
{
    private readonly Exception? _excepcion;

    public CorregirFechaInicioVinculacion? ComandoRecibido { get; private set; }

    public FakeCorregirFechaInicioVinculacionCommandRouter(Exception? lanzar = null) =>
        _excepcion = lanzar;

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (command is CorregirFechaInicioVinculacion corregirFechaInicioVinculacion)
            ComandoRecibido = corregirFechaInicioVinculacion;

        if (_excepcion is not null)
            throw _excepcion;

        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(
        TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
