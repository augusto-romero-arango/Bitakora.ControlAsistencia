// Issue #330: tests del endpoint HTTP POST Colaboradores (registrar colaborador).
// MEF-ADR-0004: InvalidOperationException -> 409, exito -> 202. Precedente: CrearTurnoFunction.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ComandoRegistrarColaborador = Bitakora.ControlAsistencia.Colaboradores.RegistrarColaborador.RegistrarColaborador;
using FunctionEndpoint = Bitakora.ControlAsistencia.Colaboradores.RegistrarColaborador.FunctionEndpoint;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.RegistrarColaborador;

public class FunctionEndpointTests
{
    private static ComandoRegistrarColaborador ComandoValido() => new(
        TipoIdentificacion: "CC",
        NumeroIdentificacion: "79543210",
        PrimerNombre: "Luis",
        SegundoNombre: null,
        PrimerApellido: "Barreto",
        SegundoApellido: null,
        CodigoColaborador: "COL-001",
        FechaInicio: new DateOnly(2026, 1, 15));

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    // CA-1: POST exitoso retorna 202 Accepted
    [Fact]
    public async Task RegistrarColaborador_Retorna202_CuandoComandoEsValido()
    {
        var validator = new FakeRequestValidator<ComandoRegistrarColaborador>(ComandoValido());
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-2: POST con identificacion ya registrada retorna 409 Conflict
    [Fact]
    public async Task RegistrarColaborador_Retorna409_CuandoIdentificacionYaExiste()
    {
        var validator = new FakeRequestValidator<ComandoRegistrarColaborador>(ComandoValido());
        var router = new FakeCommandRouter(lanzarInvalidOperationException: true);
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // CA-3: POST con JSON invalido o campos faltantes retorna 400 Bad Request
    [Fact]
    public async Task RegistrarColaborador_Retorna400_CuandoRequestEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeRequestValidator<ComandoRegistrarColaborador>(error: errorDeValidacion);
        var router = new FakeCommandRouter();
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
internal class FakeRequestValidator<TComando> : IRequestValidator
{
    private readonly TComando? _comando;
    private readonly IActionResult? _error;

    public FakeRequestValidator(TComando? comando = default, IActionResult? error = null)
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
/// Fake configurable de ICommandRouter. Puede configurarse para completar exitosamente o lanzar
/// InvalidOperationException (identificacion duplicada).
/// </summary>
internal class FakeCommandRouter : ICommandRouter
{
    private readonly bool _lanzarInvalidOperation;

    public FakeCommandRouter(bool lanzarInvalidOperationException = false) =>
        _lanzarInvalidOperation = lanzarInvalidOperationException;

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (_lanzarInvalidOperation)
            throw new InvalidOperationException("El colaborador ya existe");

        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(
        TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
