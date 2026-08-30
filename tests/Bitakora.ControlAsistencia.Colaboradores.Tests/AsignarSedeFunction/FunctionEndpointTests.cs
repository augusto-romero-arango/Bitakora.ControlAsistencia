// Issue #465 (MEF-ADR-0043 paso 2): tests del endpoint HTTP PUT colaboradores/{id}/sede (asignar o
// reasignar la sede del colaborador). CA-1: 202; id de ruta invalido -> 400 (parseo tipado unico,
// precedente ObtenerFichaColaborador); CA-ADR-0030 / MEF-ADR-0004: InvalidOperationException -> 409,
// KeyNotFoundException -> 404.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.AsignarSedeFunction;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AsignarSedeFunction;

public class FunctionEndpointTests
{
    private const string IdValido = "CC-79543210";
    private const string CodigoSedeValido = "BOG";

    private static AsignarSedeBody BodyValido() => new(CodigoSedeValido);

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    // CA-1: PUT exitoso retorna 202 Accepted
    [Fact]
    public async Task AsignarSede_Retorna202_CuandoIdDeRutaYBodySonValidos()
    {
        var validator = new FakeAsignarSedeBodyRequestValidator(BodyValido());
        var router = new FakeAsignarSedeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-1: el endpoint compone el comando interno AsignarSede desde {id} + CodigoSede del body --
    // el router debe recibir exactamente esos 3 campos primitivos (MEF-ADR-0039 decision 6), tipo y
    // numero derivados de Identificacion.Parsear.
    [Fact]
    public async Task AsignarSede_ComponeElComando_DesdeIdDeRutaYCodigoSedeDelBody()
    {
        var validator = new FakeAsignarSedeBodyRequestValidator(BodyValido());
        var router = new FakeAsignarSedeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        router.ComandoRecibido.Should().Be(new AsignarSede(
            TipoIdentificacion: "CC",
            NumeroIdentificacion: "79543210",
            CodigoSede: CodigoSedeValido));
    }

    // id de ruta sin guion -> 400, sin llegar a invocar el router (el parseo tipado es el unico
    // punto de traduccion, precedente ObtenerFichaColaborador.FunctionEndpoint).
    [Fact]
    public async Task AsignarSede_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var validator = new FakeAsignarSedeBodyRequestValidator(BodyValido());
        var router = new FakeAsignarSedeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "CC79543210", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con un id invalido");
    }

    // tipo fuera de la lista cerrada PILA -> 400.
    [Fact]
    public async Task AsignarSede_Retorna400_CuandoElTipoDeLaIdentificacionNoEstaEnLaListaCerrada()
    {
        var validator = new FakeAsignarSedeBodyRequestValidator(BodyValido());
        var router = new FakeAsignarSedeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "XX-79543210", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // CodigoSede vacio en el body -> 400 Bad Request
    [Fact]
    public async Task AsignarSede_Retorna400_CuandoElBodyEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeAsignarSedeBodyRequestValidator(error: errorDeValidacion);
        var router = new FakeAsignarSedeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // CA-4: violacion de la regla de apertura (vinculacion con terminacion registrada) retorna 409
    // Conflict (MEF-ADR-0043 seccion 2 paso 2: el 409 de un PUT es una instancia mas de "declinar
    // con resultado").
    [Fact]
    public async Task AsignarSede_Retorna409_CuandoLaVinculacionTieneTerminacionRegistrada()
    {
        var validator = new FakeAsignarSedeBodyRequestValidator(BodyValido());
        var router = new FakeAsignarSedeCommandRouter(
            lanzar: new InvalidOperationException(
                "La vinculacion vigente del colaborador tiene una terminacion registrada"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // CA-6: colaborador inexistente retorna 404 Not Found
    [Fact]
    public async Task AsignarSede_Retorna404_CuandoColaboradorNoExiste()
    {
        var validator = new FakeAsignarSedeBodyRequestValidator(BodyValido());
        var router = new FakeAsignarSedeCommandRouter(
            lanzar: new KeyNotFoundException("No existe un colaborador registrado con esa identificacion"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}

// ---- Fakes manuales - NO NSubstitute ----

/// <summary>
/// Fake configurable de IRequestValidator para el body reducido (AsignarSedeBody). Retorna un body
/// pre-configurado o un error segun lo que se le pase en el constructor.
/// </summary>
internal class FakeAsignarSedeBodyRequestValidator : IRequestValidator
{
    private readonly AsignarSedeBody? _body;
    private readonly IActionResult? _error;

    public FakeAsignarSedeBodyRequestValidator(
        AsignarSedeBody? body = null,
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
internal class FakeAsignarSedeCommandRouter : ICommandRouter
{
    private readonly Exception? _excepcion;

    public AsignarSede? ComandoRecibido { get; private set; }

    public FakeAsignarSedeCommandRouter(Exception? lanzar = null) =>
        _excepcion = lanzar;

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (command is AsignarSede asignarSede)
            ComandoRecibido = asignarSede;

        if (_excepcion is not null)
            throw _excepcion;

        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(
        TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
