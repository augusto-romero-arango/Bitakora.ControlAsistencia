// Issue #457: tests del endpoint HTTP PUT sedes/{codigo}/ubicacion (actualizar ubicacion de sede).
// MEF-ADR-0004: KeyNotFoundException -> 404, exito -> 202. Precedente: CorregirNombresFunction.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.ActualizarUbicacionSedeFunction;
using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Sedes.Tests.ActualizarUbicacionSedeFunction;

public class FunctionEndpointTests
{
    private const string Codigo = "SEDE-001";

    private static ActualizarUbicacionSedeBody BodyValido() => new("Medellin", "Carrera 50 # 20-30");

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    // CA-3: PUT exitoso retorna 202 Accepted
    [Fact]
    public async Task ActualizarUbicacionSede_Retorna202_CuandoComandoEsValido()
    {
        var validator = new FakeRequestValidator<ActualizarUbicacionSedeBody>(BodyValido());
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-4: PUT sobre una sede inexistente retorna 404 Not Found
    [Fact]
    public async Task ActualizarUbicacionSede_Retorna404_CuandoSedeNoExiste()
    {
        var validator = new FakeRequestValidator<ActualizarUbicacionSedeBody>(BodyValido());
        var router = new FakeCommandRouter(lanzarKeyNotFoundException: true);
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // Body malformado (JSON invalido) retorna 400 Bad Request -- sin validator de forma (ambos
    // campos son opcionales), pero RequestValidator.ValidarAsync sigue rechazando un body que no
    // deserializa.
    [Fact]
    public async Task ActualizarUbicacionSede_Retorna400_CuandoRequestEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeRequestValidator<ActualizarUbicacionSedeBody>(error: errorDeValidacion);
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}

// ---- Fakes manuales - NO NSubstitute ----

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

internal class FakeCommandRouter : ICommandRouter
{
    private readonly bool _lanzarKeyNotFound;

    public FakeCommandRouter(bool lanzarKeyNotFoundException = false) =>
        _lanzarKeyNotFound = lanzarKeyNotFoundException;

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (_lanzarKeyNotFound)
            throw new KeyNotFoundException("La sede no existe");

        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(
        TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
