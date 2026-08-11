// Issue #351: tests del endpoint HTTP POST Colaboradores/Nombres (corregir nombres). Sin caso 409
// (CA-ADR-0030: este comando no tiene reglas de estado) -- solo 400 (validacion), 404 (colaborador
// inexistente) y 202 (exito, con o sin evento nuevo en el stream). Precedente:
// TerminarVinculacionFunction.FunctionEndpoint.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.CorregirNombresFunction;

public class FunctionEndpointTests
{
    private static CorregirNombres ComandoValido() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: "79543210",
        PrimerNombre: "Luis",
        SegundoNombre: "Augusto",
        PrimerApellido: "Barreto",
        SegundoApellido: null);

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    // CA-1/CA-2/CA-3: POST exitoso (con o sin evento nuevo -- la idempotencia silenciosa del
    // aggregate no cambia el codigo HTTP) retorna 202 Accepted.
    [Fact]
    public async Task CorregirNombres_Retorna202_CuandoComandoEsValido()
    {
        var validator = new FakeCorregirNombresRequestValidator(ComandoValido());
        var router = new FakeCorregirNombresCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-4: colaborador inexistente retorna 404 Not Found
    [Fact]
    public async Task CorregirNombres_Retorna404_CuandoColaboradorNoExiste()
    {
        var validator = new FakeCorregirNombresRequestValidator(ComandoValido());
        var router = new FakeCorregirNombresCommandRouter(
            lanzar: new KeyNotFoundException("No existe un colaborador registrado con esa identificacion"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // CA-5: request invalida (sin primer nombre, sin primer apellido, sin identificacion, tipo
    // fuera de la lista) retorna 400 Bad Request
    [Fact]
    public async Task CorregirNombres_Retorna400_CuandoRequestEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeCorregirNombresRequestValidator(error: errorDeValidacion);
        var router = new FakeCorregirNombresCommandRouter();
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
internal class FakeCorregirNombresRequestValidator : IRequestValidator
{
    private readonly CorregirNombres? _comando;
    private readonly IActionResult? _error;

    public FakeCorregirNombresRequestValidator(
        CorregirNombres? comando = null,
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
/// configurada (KeyNotFoundException -> 404). Sin caso InvalidOperationException/409: este
/// comando no tiene reglas de estado (CA-ADR-0030).
/// </summary>
internal class FakeCorregirNombresCommandRouter : ICommandRouter
{
    private readonly Exception? _excepcion;

    public FakeCorregirNombresCommandRouter(Exception? lanzar = null) =>
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
