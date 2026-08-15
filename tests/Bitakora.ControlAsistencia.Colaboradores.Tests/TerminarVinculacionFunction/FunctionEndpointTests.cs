// Issue #379 (MEF-ADR-0043 paso 4): tests del endpoint HTTP POST
// colaboradores/{id}/vinculaciones/{codigo}:terminar (terminar la vinculacion vigente de un
// colaborador, ahora direccionada por su codigo). {id} se parsea via Identificacion.Parsear (unico
// punto de conversion, MEF-ADR-0037); {codigo} viaja intacto al comando -- la comparacion contra el
// codigo vigente vive en el aggregate. El body se reduce a FechaEfectiva
// (TerminarVinculacionBody). El comando interno TerminarVinculacion conserva sus 4 campos (mismo
// criterio que CorregirNombres/IniciarVinculacion post-#377/#378): el endpoint lo compone desde
// ruta + body.
// CA-2: 202, con composicion exacta del comando interno desde {id} + {codigo} + body; CA-3/CA-4/
// CA-5: reglas de estado y de codigo conservadas -> 409; CA-6: colaborador inexistente -> 404,
// {id} de ruta invalido -> 400 (precedente CorregirNombresFunction.FunctionEndpoint post-#377),
// body invalido -> 400.
// Reemplaza el POST Colaboradores/Terminaciones (issue #349): la ruta vieja deja de existir (CA-7,
// verificado por la ausencia de esa ruta en este archivo).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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

    // CA-2: POST exitoso retorna 202 Accepted
    [Fact]
    public async Task TerminarVinculacion_Retorna202_CuandoIdDeRutaCodigoYBodySonValidos()
    {
        var validator = new FakeTerminarVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeTerminarVinculacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CodigoValido, CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-2: el endpoint compone el comando interno TerminarVinculacion desde {id} + {codigo} + el
    // campo del body -- el router debe recibir exactamente esos 4 campos primitivos (MEF-ADR-0039
    // decision 6), tipo y numero derivados de Identificacion.Parsear.
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

    // CA-6: id de ruta sin guion -> 400, sin llegar a invocar el router (el parseo tipado es el
    // unico punto de traduccion, precedente CorregirNombresFunction.FunctionEndpoint post-#377).
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

    // CA-6: tipo de identificacion fuera de la lista cerrada PILA -> 400.
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

    // CA-6: numero vacio tras el guion del {id} de ruta -> 400.
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

    // CA-6: body invalido (sin FechaEfectiva) retorna 400 Bad Request
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

    // CA-3/CA-4/CA-5: violacion de una regla de negocio (ya terminada / fecha anterior al inicio /
    // codigo no corresponde) retorna 409 Conflict.
    [Fact]
    public async Task TerminarVinculacion_Retorna409_CuandoLaVinculacionYaTieneTerminacionRegistrada()
    {
        var validator = new FakeTerminarVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeTerminarVinculacionCommandRouter(
            lanzar: new InvalidOperationException("La vinculacion ya tiene una terminacion registrada"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CodigoValido, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // CA-6: colaborador inexistente retorna 404 Not Found
    [Fact]
    public async Task TerminarVinculacion_Retorna404_CuandoColaboradorNoExiste()
    {
        var validator = new FakeTerminarVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeTerminarVinculacionCommandRouter(
            lanzar: new KeyNotFoundException("No existe un colaborador registrado con esa identificacion"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CodigoValido, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}

// ---- Fakes manuales - NO NSubstitute ----

/// <summary>
/// Fake configurable de IRequestValidator para el body reducido (TerminarVinculacionBody). Retorna
/// un body pre-configurado o un error segun lo que se le pase en el constructor.
/// </summary>
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

/// <summary>
/// Fake configurable de ICommandRouter. Registra el comando recibido (ComandoRecibido) para
/// verificar la composicion ruta+body, y puede completar exitosamente o lanzar la excepcion
/// configurada (InvalidOperationException -> 409, KeyNotFoundException -> 404).
/// </summary>
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
