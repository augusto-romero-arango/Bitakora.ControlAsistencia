// El 409 lo traduce el endpoint desde la InvalidOperationException del handler (CA-ADR-0030);
// el formato de fecha es el mismo que valida ObtenerDepuracionDelDia.FunctionEndpoint.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.AprobarDiaFunction;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

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

    // 204 sin cuerpo: dia_aprobado ya quedo durable al responder (MEF-ADR-0043 paso 4).
    [Fact]
    public async Task AprobarDia_Retorna204SinCuerpo_CuandoFechaYBodySonValidos()
    {
        var validator = new FakeAprobarDiaBodyRequestValidator(BodySinDecisiones());
        var router = new FakeAprobarDiaCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(
            FakeHttpRequest(), CodigoColaboradorValido, FechaValida, CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        result.Should().NotBeAssignableTo<ObjectResult>();
    }

    [Fact]
    public async Task AprobarDia_ComponeElComando_DesdeRutaYBody()
    {
        var decision = new DecisionDeSede(new TimeOnly(6, 0), "SEDE-02");
        var validator = new FakeAprobarDiaBodyRequestValidator(new AprobarDiaBody([decision]));
        var router = new FakeAprobarDiaCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        await function.Run(FakeHttpRequest(), CodigoColaboradorValido, FechaValida, CancellationToken.None);

        router.ComandoRecibido.Should().BeEquivalentTo(new AprobarDia(
            CodigoColaboradorValido, new DateOnly(2026, 8, 24), [decision]));
    }

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
