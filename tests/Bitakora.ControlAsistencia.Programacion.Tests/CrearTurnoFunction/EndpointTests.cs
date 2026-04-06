// HU-4: Tests del endpoint HTTP CrearTurno

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CrearTurno = Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.CrearTurno;
using Endpoint = Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.Endpoint;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoFunction;

/// <summary>
/// Tests del endpoint HTTP POST /programacion/turnos.
/// Verifica que el endpoint mapea correctamente los resultados del handler a respuestas HTTP.
/// ADR-0007: InvalidOperationException -> 409, AggregateException -> 400, exito -> 202.
/// </summary>
public class EndpointTests
{
    private static CrearTurno.Franja FranjaDiurnaSimple() =>
        new(new TimeOnly(8, 0), new TimeOnly(16, 0), [], []);

    private static CrearTurno ComandoValido() =>
        new(Guid.NewGuid(), "Turno Manana", [FranjaDiurnaSimple()]);

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    // CA-7: POST exitoso retorna 202 Accepted
    [Fact]
    public async Task DebeRetornar202_CuandoComandoEsValido()
    {
        var validator = new FakeRequestValidator<CrearTurno>(ComandoValido());
        var router = new FakeCommandRouter();
        var function = new Endpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-8: POST con TurnoId duplicado retorna 409 Conflict
    [Fact]
    public async Task DebeRetornar409_CuandoTurnoYaExiste()
    {
        var validator = new FakeRequestValidator<CrearTurno>(ComandoValido());
        var router = new FakeCommandRouter(lanzarInvalidOperationException: true);
        var function = new Endpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // CA-9: POST con JSON invalido o campos faltantes retorna 400 Bad Request
    [Fact]
    public async Task DebeRetornar400_CuandoRequestEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeRequestValidator<CrearTurno>(error: errorDeValidacion);
        var router = new FakeCommandRouter();
        var function = new Endpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // CA-10: POST con franjas invalidas (AggregateException del factory) retorna 400 con mensajes
    [Fact]
    public async Task DebeRetornar400ConMensajes_CuandoFranjasDelComandoSonInvalidas()
    {
        var validator = new FakeRequestValidator<CrearTurno>(ComandoValido());
        var erroresDeNegocio = new ArgumentException[] { new("La franja ordinaria es invalida") };
        var router = new FakeCommandRouter(erroresAggregateException: erroresDeNegocio);
        var function = new Endpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}

// ---- Fakes manuales - NO NSubstitute ----

/// <summary>
/// Fake configurable de IRequestValidator. Retorna un comando pre-configurado
/// o un error segun lo que se le pase en el constructor.
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
/// Fake configurable de ICommandRouter. Puede configurarse para completar exitosamente,
/// lanzar InvalidOperationException (turno duplicado) o AggregateException (franjas invalidas).
/// </summary>
internal class FakeCommandRouter : ICommandRouter
{
    private readonly bool _lanzarInvalidOperation;
    private readonly ArgumentException[]? _erroresAggregate;

    public FakeCommandRouter(
        bool lanzarInvalidOperationException = false,
        ArgumentException[]? erroresAggregateException = null)
    {
        _lanzarInvalidOperation = lanzarInvalidOperationException;
        _erroresAggregate = erroresAggregateException;
    }

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (_lanzarInvalidOperation)
            throw new InvalidOperationException("El turno ya existe");

        if (_erroresAggregate is not null)
            throw new AggregateException(_erroresAggregate);

        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(
        TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
