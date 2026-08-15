// Issue #378 (MEF-ADR-0043 paso 1): tests del endpoint HTTP POST
// colaboradores/{id}/vinculaciones (iniciar una vinculacion nueva sobre un colaborador existente --
// create disfrazado: emite el MISMO evento que RegistrarColaborador, VinculacionIniciada). Absorbe
// y reemplaza a ReingresarColaboradorFunction/FunctionEndpointTests.cs (issue #350): {id} se parsea
// via Identificacion.Parsear (unico punto de conversion, MEF-ADR-0037), el body se reduce a
// CodigoColaborador + FechaInicio (IniciarVinculacionBody). El comando interno IniciarVinculacion
// conserva sus 4 campos (mismo criterio que CorregirNombres post-#377): el endpoint lo compone
// desde ruta + body.
// CA-1: 202, con composicion exacta del comando interno desde {id} + body; CA-2: reglas de estado
// conservadas -> 409 (vinculacion abierta / fecha solapa); CA-3: colaborador inexistente -> 404,
// {id} de ruta invalido -> 400 (precedente CorregirNombresFunction.FunctionEndpoint post-#377),
// body invalido -> 400.
// Reemplaza el POST Colaboradores/Reingresos (issue #350): la ruta vieja deja de existir (CA-6,
// verificado por la ausencia de esa ruta en este archivo).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.IniciarVinculacionFunction;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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

    // CA-1: POST exitoso retorna 202 Accepted
    [Fact]
    public async Task IniciarVinculacion_Retorna202_CuandoIdDeRutaYBodySonValidos()
    {
        var validator = new FakeIniciarVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeIniciarVinculacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-1: el endpoint compone el comando interno IniciarVinculacion desde {id} + los 2 campos del
    // body -- el router debe recibir exactamente esos 4 campos primitivos (MEF-ADR-0039 decision
    // 6), tipo y numero derivados de Identificacion.Parsear.
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

    // CA-3: id de ruta sin guion -> 400, sin llegar a invocar el router (el parseo tipado es el
    // unico punto de traduccion, precedente CorregirNombresFunction.FunctionEndpoint post-#377).
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

    // CA-3: tipo de identificacion fuera de la lista cerrada PILA -> 400.
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

    // CA-3: numero vacio tras el guion del {id} de ruta -> 400.
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

    // CA-3: body invalido (CodigoColaborador vacio o FechaInicio default) -> 400 Bad Request
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

    // CA-2: violacion de la invariante de no-solape (vinculacion abierta / fecha solapa la
    // anterior) retorna 409 Conflict.
    [Fact]
    public async Task IniciarVinculacion_Retorna409_CuandoLaVinculacionVigenteEstaAbierta()
    {
        var validator = new FakeIniciarVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeIniciarVinculacionCommandRouter(
            lanzar: new InvalidOperationException(
                "La vinculacion vigente del colaborador no tiene una terminacion registrada"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // CA-3: colaborador inexistente retorna 404 Not Found
    [Fact]
    public async Task IniciarVinculacion_Retorna404_CuandoColaboradorNoExiste()
    {
        var validator = new FakeIniciarVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeIniciarVinculacionCommandRouter(
            lanzar: new KeyNotFoundException("No existe un colaborador registrado con esa identificacion"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}

// ---- Fakes manuales - NO NSubstitute ----

/// <summary>
/// Fake configurable de IRequestValidator para el body reducido (IniciarVinculacionBody). Retorna
/// un body pre-configurado o un error segun lo que se le pase en el constructor.
/// </summary>
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

/// <summary>
/// Fake configurable de ICommandRouter. Registra el comando recibido (ComandoRecibido) para
/// verificar la composicion ruta+body, y puede completar exitosamente o lanzar la excepcion
/// configurada (InvalidOperationException -> 409, KeyNotFoundException -> 404).
/// </summary>
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
