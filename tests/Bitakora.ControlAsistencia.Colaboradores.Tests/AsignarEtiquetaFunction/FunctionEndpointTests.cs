// Issue #355: tests del endpoint HTTP POST Colaboradores/Etiquetas (asignar etiqueta).
// CA-ADR-0030 / MEF-ADR-0004: InvalidOperationException -> 409, KeyNotFoundException -> 404,
// exito -> 202. Precedente: AnularTerminacionFunction.FunctionEndpoint.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AsignarEtiquetaFunction;

public class FunctionEndpointTests
{
    private static AsignarEtiqueta ComandoValido() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: "79543210",
        Categoria: "Área",
        Valor: "Tecnología");

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    // CA-1/CA-2: POST exitoso retorna 202 Accepted
    [Fact]
    public async Task AsignarEtiqueta_Retorna202_CuandoComandoEsValido()
    {
        var validator = new FakeAsignarEtiquetaRequestValidator(ComandoValido());
        var router = new FakeAsignarEtiquetaCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-5: violacion de la regla de apertura (vinculacion con terminacion registrada) retorna 409
    // Conflict.
    [Fact]
    public async Task AsignarEtiqueta_Retorna409_CuandoLaVinculacionTieneTerminacionRegistrada()
    {
        var validator = new FakeAsignarEtiquetaRequestValidator(ComandoValido());
        var router = new FakeAsignarEtiquetaCommandRouter(
            lanzar: new InvalidOperationException(
                "La vinculacion vigente del colaborador tiene una terminacion registrada"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // CA-7: colaborador inexistente retorna 404 Not Found
    [Fact]
    public async Task AsignarEtiqueta_Retorna404_CuandoColaboradorNoExiste()
    {
        var validator = new FakeAsignarEtiquetaRequestValidator(ComandoValido());
        var router = new FakeAsignarEtiquetaCommandRouter(
            lanzar: new KeyNotFoundException("No existe un colaborador registrado con esa identificacion"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // CA-7: request invalida (categoria o valor vacios, identificacion incompleta, tipo fuera de
    // la lista) retorna 400 Bad Request
    [Fact]
    public async Task AsignarEtiqueta_Retorna400_CuandoRequestEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeAsignarEtiquetaRequestValidator(error: errorDeValidacion);
        var router = new FakeAsignarEtiquetaCommandRouter();
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
internal class FakeAsignarEtiquetaRequestValidator : IRequestValidator
{
    private readonly AsignarEtiqueta? _comando;
    private readonly IActionResult? _error;

    public FakeAsignarEtiquetaRequestValidator(
        AsignarEtiqueta? comando = null,
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
internal class FakeAsignarEtiquetaCommandRouter : ICommandRouter
{
    private readonly Exception? _excepcion;

    public FakeAsignarEtiquetaCommandRouter(Exception? lanzar = null) =>
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
