// Issue #355: tests del endpoint HTTP POST Colaboradores/Etiquetas/Retiros (retirar etiqueta).
// CA-ADR-0030 / MEF-ADR-0004: InvalidOperationException -> 409, KeyNotFoundException -> 404,
// exito -> 202. Precedente: AnularTerminacionFunction.FunctionEndpoint.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Bitakora.ControlAsistencia.Colaboradores.RetirarEtiquetaFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.RetirarEtiquetaFunction;

public class FunctionEndpointTests
{
    private static RetirarEtiqueta ComandoValido() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: "79543210",
        Categoria: "Área");

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    // CA-3: POST exitoso retorna 202 Accepted
    [Fact]
    public async Task RetirarEtiqueta_Retorna202_CuandoComandoEsValido()
    {
        var validator = new FakeRetirarEtiquetaRequestValidator(ComandoValido());
        var router = new FakeRetirarEtiquetaCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-4/CA-5: categoria inexistente o vinculacion con terminacion registrada retorna 409
    // Conflict.
    [Fact]
    public async Task RetirarEtiqueta_Retorna409_CuandoLaCategoriaNoExisteOLaVinculacionEstaTerminada()
    {
        var validator = new FakeRetirarEtiquetaRequestValidator(ComandoValido());
        var router = new FakeRetirarEtiquetaCommandRouter(
            lanzar: new InvalidOperationException("No existe una etiqueta asignada con esa categoria"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // CA-7: colaborador inexistente retorna 404 Not Found
    [Fact]
    public async Task RetirarEtiqueta_Retorna404_CuandoColaboradorNoExiste()
    {
        var validator = new FakeRetirarEtiquetaRequestValidator(ComandoValido());
        var router = new FakeRetirarEtiquetaCommandRouter(
            lanzar: new KeyNotFoundException("No existe un colaborador registrado con esa identificacion"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // CA-7: request invalida (categoria vacia, identificacion incompleta, tipo fuera de la lista)
    // retorna 400 Bad Request
    [Fact]
    public async Task RetirarEtiqueta_Retorna400_CuandoRequestEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeRetirarEtiquetaRequestValidator(error: errorDeValidacion);
        var router = new FakeRetirarEtiquetaCommandRouter();
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
internal class FakeRetirarEtiquetaRequestValidator : IRequestValidator
{
    private readonly RetirarEtiqueta? _comando;
    private readonly IActionResult? _error;

    public FakeRetirarEtiquetaRequestValidator(
        RetirarEtiqueta? comando = null,
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
internal class FakeRetirarEtiquetaCommandRouter : ICommandRouter
{
    private readonly Exception? _excepcion;

    public FakeRetirarEtiquetaCommandRouter(Exception? lanzar = null) =>
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
