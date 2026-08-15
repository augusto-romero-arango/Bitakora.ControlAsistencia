// Issue #352: tests del endpoint HTTP POST Colaboradores/FechasInicio (corregir la fecha de
// inicio de la ultima vinculacion de un colaborador). CA-ADR-0030 / MEF-ADR-0004:
// InvalidOperationException -> 409, KeyNotFoundException -> 404, exito -> 202. Precedente:
// IniciarVinculacionFunction.FunctionEndpoint.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.CorregirFechaInicioVinculacionFunction;

public class FunctionEndpointTests
{
    private static CorregirFechaInicioVinculacion ComandoValido() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: "79543210",
        FechaCorregida: new DateOnly(2026, 1, 10));

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    // CA-1/CA-2/CA-4: POST exitoso retorna 202 Accepted
    [Fact]
    public async Task CorregirFechaInicioVinculacion_Retorna202_CuandoComandoEsValido()
    {
        var validator = new FakeCorregirFechaInicioVinculacionRequestValidator(ComandoValido());
        var router = new FakeCorregirFechaInicioVinculacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-2/CA-3: violacion de una regla de estado (coherencia interna / no-solape hacia atras)
    // retorna 409 Conflict.
    [Fact]
    public async Task CorregirFechaInicioVinculacion_Retorna409_CuandoLaFechaCorregidaVulneraUnaRegla()
    {
        var validator = new FakeCorregirFechaInicioVinculacionRequestValidator(ComandoValido());
        var router = new FakeCorregirFechaInicioVinculacionCommandRouter(
            lanzar: new InvalidOperationException(
                "La fecha de inicio corregida no puede ser posterior a la fecha efectiva de terminacion de la vinculacion"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // CA-5: colaborador inexistente retorna 404 Not Found
    [Fact]
    public async Task CorregirFechaInicioVinculacion_Retorna404_CuandoColaboradorNoExiste()
    {
        var validator = new FakeCorregirFechaInicioVinculacionRequestValidator(ComandoValido());
        var router = new FakeCorregirFechaInicioVinculacionCommandRouter(
            lanzar: new KeyNotFoundException("No existe un colaborador registrado con esa identificacion"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // CA-6: request invalida (sin FechaCorregida, sin identificacion, tipo fuera de la lista
    // cerrada) retorna 400 Bad Request
    [Fact]
    public async Task CorregirFechaInicioVinculacion_Retorna400_CuandoRequestEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeCorregirFechaInicioVinculacionRequestValidator(error: errorDeValidacion);
        var router = new FakeCorregirFechaInicioVinculacionCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}

// ---- Fakes manuales - NO NSubstitute ----

/// <summary>
/// Fake configurable de IRequestValidator. Retorna un comando pre-configurado o un error segun lo
/// que se le pase en el constructor.
/// </summary>
internal class FakeCorregirFechaInicioVinculacionRequestValidator : IRequestValidator
{
    private readonly CorregirFechaInicioVinculacion? _comando;
    private readonly IActionResult? _error;

    public FakeCorregirFechaInicioVinculacionRequestValidator(
        CorregirFechaInicioVinculacion? comando = null,
        IActionResult? error = null)
    {
        _comando = comando;
        _error = error;
    }

    public Task<(T? Comando, IActionResult? Error)> ValidarAsync<T>(
        HttpRequest req, CancellationToken ct)
    {
        if (_error is not null)
            return Task.FromResult<(T?, IActionResult?)>((default, _error));

        if (_comando is T resultado)
            return Task.FromResult<(T?, IActionResult?)>((resultado, null));

        return Task.FromResult<(T?, IActionResult?)>((default, null));
    }
}

/// <summary>
/// Fake configurable de ICommandRouter. Puede completar exitosamente o lanzar la excepcion
/// configurada (InvalidOperationException -> 409, KeyNotFoundException -> 404).
/// </summary>
internal class FakeCorregirFechaInicioVinculacionCommandRouter : ICommandRouter
{
    private readonly Exception? _excepcion;

    public FakeCorregirFechaInicioVinculacionCommandRouter(Exception? lanzar = null) =>
        _excepcion = lanzar;

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (_excepcion is not null)
            throw _excepcion;

        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(
        TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
