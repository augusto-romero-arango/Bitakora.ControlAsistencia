// HU-10: Solicitar programacion de turno del catalogo - tests del endpoint HTTP

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.Empleados.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Programacion.Tests.SolicitarProgramacionTurnoFunction;

/// <summary>
/// Tests del endpoint HTTP POST /programacion/solicitudes.
/// Verifica el mapeo de excepciones del handler a respuestas HTTP:
/// - InvalidOperationException -> 409 (solicitud duplicada)
/// - KeyNotFoundException -> 404 (turno no encontrado en catalogo)
/// - Exito -> 202 Accepted
/// - Error de validacion -> 400 Bad Request
/// </summary>
public class FunctionEndpointTests
{
    private static SolicitarProgramacionTurno ComandoValido() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        new InformacionEmpleado("E001", "CC", "12345678", "Juan", "Perez"),
        [new DateOnly(2026, 4, 7)]);

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    // CA-13: POST exitoso retorna 202 Accepted
    [Fact]
    public async Task DebeRetornar202_CuandoComandoEsValido()
    {
        var validator = new FakeSolicitudRequestValidator(ComandoValido());
        var router = new FakeSolicitudCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-5: Falla de validacion retorna 400 Bad Request
    [Fact]
    public async Task DebeRetornar400_CuandoFallaValidacion()
    {
        var errorDeValidacion = new BadRequestObjectResult("Campos requeridos faltantes");
        var validator = new FakeSolicitudRequestValidator(error: errorDeValidacion);
        var router = new FakeSolicitudCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // CA-6: Solicitud ya existe retorna 409 Conflict
    [Fact]
    public async Task DebeRetornar409_CuandoSolicitudYaExiste()
    {
        var validator = new FakeSolicitudRequestValidator(ComandoValido());
        var router = new FakeSolicitudCommandRouter(
            lanzar: new InvalidOperationException("La solicitud ya existe"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // CA-7: Turno no encontrado en el catalogo retorna 404 Not Found
    [Fact]
    public async Task DebeRetornar404_CuandoTurnoNoExisteEnElCatalogo()
    {
        var validator = new FakeSolicitudRequestValidator(ComandoValido());
        var router = new FakeSolicitudCommandRouter(
            lanzar: new KeyNotFoundException("Turno no encontrado"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}

// ---- Fakes manuales - NO NSubstitute ----

internal class FakeSolicitudRequestValidator : IRequestValidator
{
    private readonly SolicitarProgramacionTurno? _comando;
    private readonly IActionResult? _error;

    public FakeSolicitudRequestValidator(
        SolicitarProgramacionTurno? comando = null,
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

internal class FakeSolicitudCommandRouter : ICommandRouter
{
    private readonly Exception? _excepcion;

    public FakeSolicitudCommandRouter(Exception? lanzar = null)
    {
        _excepcion = lanzar;
    }

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
