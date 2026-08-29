// Issue #489 (MEF-ADR-0043 paso 4): tests del endpoint HTTP POST
// control-horas/depuraciones/{codigoColaborador}/{fecha}:aprobar. CA-1/CA-7: 202 con Decisiones
// vacia o ausente; CA-3..CA-6: 409 Conflict via InvalidOperationException (CA-ADR-0030); fecha con
// formato invalido -> 400, mismo criterio que ObtenerDepuracionDelDia.FunctionEndpoint.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.AprobarDiaFunction;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AprobarDiaFunction;

public class FunctionEndpointTests
{
    private const string CodigoColaboradorValido = "EMP-001";
    private const string FechaValida = "2026-08-24";

    private static AprobarDiaBody BodySinDecisiones() => new(Decisiones: null);

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    // CA-1/CA-7: POST exitoso retorna 202 Accepted.
    [Fact]
    public async Task AprobarDia_Retorna202_CuandoFechaYBodySonValidos()
    {
        var validator = new FakeAprobarDiaBodyRequestValidator(BodySinDecisiones());
        var router = new FakeAprobarDiaCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(
            FakeHttpRequest(), CodigoColaboradorValido, FechaValida, CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-2: el endpoint compone el comando interno desde {codigoColaborador} + {fecha} de ruta y
    // Decisiones del body.
    [Fact]
    public async Task AprobarDia_ComponeElComando_DesdeRutaYBody()
    {
        var decision = new AprobarDia.DecisionDeSede(new TimeOnly(6, 0), "SEDE-02");
        var validator = new FakeAprobarDiaBodyRequestValidator(new AprobarDiaBody([decision]));
        var router = new FakeAprobarDiaCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        await function.Run(FakeHttpRequest(), CodigoColaboradorValido, FechaValida, CancellationToken.None);

        router.ComandoRecibido.Should().BeEquivalentTo(new AprobarDia(
            CodigoColaboradorValido, new DateOnly(2026, 8, 24), [decision]));
    }

    // Fecha con formato invalido -> 400, sin llegar a invocar el router.
    [Fact]
    public async Task AprobarDia_Retorna400_CuandoLaFechaNoTieneElFormatoEsperado()
    {
        var validator = new FakeAprobarDiaBodyRequestValidator(BodySinDecisiones());
        var router = new FakeAprobarDiaCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(
            FakeHttpRequest(), CodigoColaboradorValido, "24-08-2026", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con una fecha invalida");
    }

    // Body invalido o malformado -> 400 Bad Request.
    [Fact]
    public async Task AprobarDia_Retorna400_CuandoElBodyEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeAprobarDiaBodyRequestValidator(error: errorDeValidacion);
        var router = new FakeAprobarDiaCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(
            FakeHttpRequest(), CodigoColaboradorValido, FechaValida, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // CA-3/CA-4/CA-5/CA-6: violacion de una regla de negocio (traducida por el handler a
    // InvalidOperationException) retorna 409 Conflict.
    [Fact]
    public async Task AprobarDia_Retorna409_CuandoElComandoEsRechazado()
    {
        var validator = new FakeAprobarDiaBodyRequestValidator(BodySinDecisiones());
        var router = new FakeAprobarDiaCommandRouter(
            lanzar: new InvalidOperationException("El dia ya fue aprobado; las aprobaciones son definitivas"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(
            FakeHttpRequest(), CodigoColaboradorValido, FechaValida, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }
}

// ---- Fakes manuales - NO NSubstitute ----

internal class FakeAprobarDiaBodyRequestValidator : IRequestValidator
{
    private readonly AprobarDiaBody? _body;
    private readonly IActionResult? _error;

    public FakeAprobarDiaBodyRequestValidator(AprobarDiaBody? body = null, IActionResult? error = null)
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

internal class FakeAprobarDiaCommandRouter : ICommandRouter
{
    private readonly Exception? _excepcion;

    public AprobarDia? ComandoRecibido { get; private set; }

    public FakeAprobarDiaCommandRouter(Exception? lanzar = null) => _excepcion = lanzar;

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (command is AprobarDia aprobarDia)
            ComandoRecibido = aprobarDia;

        if (_excepcion is not null)
            throw _excepcion;

        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
