// Sin caso 409: CorregirNombres no tiene reglas de estado.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction;
using Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction.CommandHandler;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.CorregirNombresFunction;

public class FunctionEndpointTests
{
    private const string IdValido = "CC-79543210";

    private static CorregirNombresBody BodyValido() => new(
        PrimerNombre: "Luis",
        SegundoNombre: "Augusto",
        PrimerApellido: "Barreto",
        SegundoApellido: null);

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    // Tambien cuando el nombre ya es el vigente (SinCambios): el endpoint no distingue ambos caminos;
    // el no-op sin evento lo cubre CorregirNombresCommandHandlerTests.
    [Fact]
    public async Task CorregirNombres_Retorna204SinCuerpo_CuandoIdDeRutaYBodySonValidos()
    {
        var validator = new FakeCorregirNombresBodyRequestValidator(BodyValido());
        var router = new FakeCorregirNombresCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        result.Should().NotBeAssignableTo<ObjectResult>();
    }

    [Fact]
    public async Task CorregirNombres_ComponeElComando_DesdeIdDeRutaYBody()
    {
        var validator = new FakeCorregirNombresBodyRequestValidator(BodyValido());
        var router = new FakeCorregirNombresCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        router.ComandoRecibido.Should().Be(new CorregirNombres(
            TipoIdentificacion: "CC",
            NumeroIdentificacion: "79543210",
            PrimerNombre: "Luis",
            SegundoNombre: "Augusto",
            PrimerApellido: "Barreto",
            SegundoApellido: null));
    }

    [Fact]
    public async Task CorregirNombres_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var validator = new FakeCorregirNombresBodyRequestValidator(BodyValido());
        var router = new FakeCorregirNombresCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "CC79543210", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con un id invalido");
    }

    [Fact]
    public async Task CorregirNombres_Retorna400_CuandoElTipoDeLaIdentificacionNoEstaEnLaListaCerrada()
    {
        var validator = new FakeCorregirNombresBodyRequestValidator(BodyValido());
        var router = new FakeCorregirNombresCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "XX-79543210", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con un id invalido");
    }

    [Fact]
    public async Task CorregirNombres_Retorna400_CuandoElNumeroDeLaIdentificacionQuedaVacio()
    {
        var validator = new FakeCorregirNombresBodyRequestValidator(BodyValido());
        var router = new FakeCorregirNombresCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "CC-", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con un id invalido");
    }

    [Fact]
    public async Task CorregirNombres_Retorna400_CuandoElBodyEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeCorregirNombresBodyRequestValidator(error: errorDeValidacion);
        var router = new FakeCorregirNombresCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CorregirNombres_Retorna404_CuandoColaboradorNoExiste()
    {
        var validator = new FakeCorregirNombresBodyRequestValidator(BodyValido());
        var router = new FakeCorregirNombresCommandRouter(
            lanzar: new RecursoNoEncontradoException(
                CorregirNombresCommandHandler.Mensajes.ColaboradorNoEncontrado));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CorregirNombres_PropagaInvalidOperationException_CuandoFallaLaInfraestructura()
    {
        var function = new FunctionEndpoint(
            new FakeCorregirNombresBodyRequestValidator(BodyValido()),
            new FakeCorregirNombresCommandRouter(new InvalidOperationException()));

        var act = async () => await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>();
    }
}

internal class FakeCorregirNombresBodyRequestValidator : IRequestValidator
{
    private readonly CorregirNombresBody? _body;
    private readonly IActionResult? _error;

    public FakeCorregirNombresBodyRequestValidator(
        CorregirNombresBody? body = null,
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

internal class FakeCorregirNombresCommandRouter : ICommandRouter
{
    private readonly Exception? _excepcion;

    public CorregirNombres? ComandoRecibido { get; private set; }

    public FakeCorregirNombresCommandRouter(Exception? lanzar = null) =>
        _excepcion = lanzar;

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (command is CorregirNombres corregirNombres)
            ComandoRecibido = corregirNombres;

        if (_excepcion is not null)
            throw _excepcion;

        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(
        TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
