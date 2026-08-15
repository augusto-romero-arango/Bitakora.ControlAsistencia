// Issue #379 (MEF-ADR-0043 paso 4): tests del endpoint HTTP POST
// colaboradores/{id}/vinculaciones/{codigo}:corregir-fecha-inicio (corregir la fecha de inicio de
// la ultima vinculacion de un colaborador, ahora direccionada por su codigo). {id} se parsea via
// Identificacion.Parsear (unico punto de conversion, MEF-ADR-0037); {codigo} viaja intacto al
// comando -- la comparacion contra el codigo vigente vive en el aggregate. El body se reduce a
// FechaCorregida (CorregirFechaInicioVinculacionBody). El comando interno
// CorregirFechaInicioVinculacion conserva sus 4 campos (mismo criterio que CorregirNombres/
// IniciarVinculacion/TerminarVinculacion post-#377/#378/#379): el endpoint lo compone desde ruta +
// body.
// CA-4: 202, con composicion exacta del comando interno desde {id} + {codigo} + body; CA-2/CA-3/
// CA-5: reglas de estado y de codigo conservadas -> 409 (incluye la idempotencia SinCambios ->
// 202); CA-6: colaborador inexistente -> 404, {id} de ruta invalido -> 400 (precedente
// CorregirNombresFunction.FunctionEndpoint post-#377), body invalido -> 400.
// Reemplaza el POST Colaboradores/FechasInicio (issue #352): la ruta vieja deja de existir (CA-7,
// verificado por la ausencia de esa ruta en este archivo).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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

    // CA-4: POST exitoso retorna 202 Accepted
    [Fact]
    public async Task CorregirFechaInicioVinculacion_Retorna202_CuandoIdDeRutaCodigoYBodySonValidos()
    {
        var validator = new FakeCorregirFechaInicioVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeCorregirFechaInicioVinculacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CodigoValido, CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-4: el endpoint compone el comando interno CorregirFechaInicioVinculacion desde {id} +
    // {codigo} + el campo del body -- el router debe recibir exactamente esos 4 campos primitivos
    // (MEF-ADR-0039 decision 6), tipo y numero derivados de Identificacion.Parsear.
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

    // CA-6: id de ruta sin guion -> 400, sin llegar a invocar el router (el parseo tipado es el
    // unico punto de traduccion, precedente CorregirNombresFunction.FunctionEndpoint post-#377).
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

    // CA-6: tipo de identificacion fuera de la lista cerrada PILA -> 400.
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

    // CA-6: numero vacio tras el guion del {id} de ruta -> 400.
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

    // CA-6: body invalido (sin FechaCorregida) retorna 400 Bad Request
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

    // CA-2/CA-3/CA-5: violacion de una regla de estado o de codigo retorna 409 Conflict.
    [Fact]
    public async Task CorregirFechaInicioVinculacion_Retorna409_CuandoLaFechaCorregidaVulneraUnaRegla()
    {
        var validator = new FakeCorregirFechaInicioVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeCorregirFechaInicioVinculacionCommandRouter(
            lanzar: new InvalidOperationException(
                "La fecha de inicio corregida no puede ser posterior a la fecha efectiva de terminacion de la vinculacion"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CodigoValido, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // CA-6: colaborador inexistente retorna 404 Not Found
    [Fact]
    public async Task CorregirFechaInicioVinculacion_Retorna404_CuandoColaboradorNoExiste()
    {
        var validator = new FakeCorregirFechaInicioVinculacionBodyRequestValidator(BodyValido());
        var router = new FakeCorregirFechaInicioVinculacionCommandRouter(
            lanzar: new KeyNotFoundException("No existe un colaborador registrado con esa identificacion"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CodigoValido, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}

// ---- Fakes manuales - NO NSubstitute ----

/// <summary>
/// Fake configurable de IRequestValidator para el body reducido (CorregirFechaInicioVinculacionBody).
/// Retorna un body pre-configurado o un error segun lo que se le pase en el constructor.
/// </summary>
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

/// <summary>
/// Fake configurable de ICommandRouter. Registra el comando recibido (ComandoRecibido) para
/// verificar la composicion ruta+body, y puede completar exitosamente o lanzar la excepcion
/// configurada (InvalidOperationException -> 409, KeyNotFoundException -> 404).
/// </summary>
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
