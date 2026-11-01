using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.Programacion.Tests.SolicitarProgramacionTurnoFunction;

public class FunctionEndpointTests
{
    private static SolicitarProgramacionTurno ComandoValido() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        new ColaboradorSolicitado("CC-12345678", "E001", "Juan Perez"),
        [new DateOnly(2026, 4, 7), new DateOnly(2026, 4, 8)]);

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    [Fact]
    public async Task SolicitarProgramacionTurno_Retorna201SinLocation_CuandoComandoEsValido()
    {
        var function = new FunctionEndpoint(
            new FakeSolicitudRequestValidator(ComandoValido()),
            new FakeSolicitudCommandRouter(resultado: new ResultadoSolicitudProgramacion([])));

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
        result.Should().BeAssignableTo<CreatedResult>()
            .Which.Location.Should().BeNull();
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_Retorna201ConLasFechasRespetadasEnElBody_CuandoHuboAusencias()
    {
        var esperado = new ResultadoSolicitudProgramacion(
            [new FechaRespetada(new DateOnly(2026, 10, 13), "Vacaciones")]);
        var function = new FunctionEndpoint(
            new FakeSolicitudRequestValidator(ComandoValido()),
            new FakeSolicitudCommandRouter(resultado: esperado));

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        var conBody = result.Should().BeAssignableTo<ObjectResult>().Subject;
        conBody.StatusCode.Should().Be(StatusCodes.Status201Created);
        conBody.Value.Should().BeEquivalentTo(esperado);
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_Retorna201ConListaVacia_CuandoNoHuboAusencias()
    {
        var esperado = new ResultadoSolicitudProgramacion([]);
        var function = new FunctionEndpoint(
            new FakeSolicitudRequestValidator(ComandoValido()),
            new FakeSolicitudCommandRouter(resultado: esperado));

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        var conBody = result.Should().BeAssignableTo<ObjectResult>().Subject;
        conBody.StatusCode.Should().Be(StatusCodes.Status201Created);
        conBody.Value.Should().BeEquivalentTo(esperado);
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_Retorna201SinLocation_CuandoElComandoTraeJornadaId()
    {
        var comando = ComandoValido() with { JornadaId = Guid.NewGuid() };
        var function = new FunctionEndpoint(
            new FakeSolicitudRequestValidator(comando),
            new FakeSolicitudCommandRouter(resultado: new ResultadoSolicitudProgramacion([])));

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
        result.Should().BeAssignableTo<CreatedResult>().Which.Location.Should().BeNull();
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_PropagaInvalidOperationException_CuandoFallaLaInfraestructura()
    {
        var fallo = new InvalidOperationException("Fallo de infraestructura");
        var function = new FunctionEndpoint(
            new FakeSolicitudRequestValidator(ComandoValido()),
            new FakeSolicitudCommandRouter(lanzar: fallo));

        var act = async () => await function.Run(FakeHttpRequest(), CancellationToken.None);

        (await act.Should().ThrowExactlyAsync<InvalidOperationException>()).Which.Should().BeSameAs(fallo);
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_RetornaElErrorDeValidacion_CuandoFallaLaValidacion()
    {
        var error = new BadRequestObjectResult("invalido");
        var function = new FunctionEndpoint(
            new FakeSolicitudRequestValidator(error: error),
            new FakeSolicitudCommandRouter());

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeSameAs(error);
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_Retorna409_CuandoSolicitudYaExiste()
    {
        var function = new FunctionEndpoint(
            new FakeSolicitudRequestValidator(ComandoValido()),
            new FakeSolicitudCommandRouter(lanzar: new RecursoYaExisteException("ya existe")));

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_Retorna409_CuandoUnaReglaDeNegocioDeclina()
    {
        var function = new FunctionEndpoint(
            new FakeSolicitudRequestValidator(ComandoValido()),
            new FakeSolicitudCommandRouter(lanzar: new ReglaDeNegocioDeclinadaException("todas con ausencia")));

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_Retorna404_CuandoElRecursoNoExiste()
    {
        var function = new FunctionEndpoint(
            new FakeSolicitudRequestValidator(ComandoValido()),
            new FakeSolicitudCommandRouter(lanzar: new RecursoNoEncontradoException("no existe")));

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}

internal class FakeSolicitudRequestValidator(
    SolicitarProgramacionTurno? comando = null,
    IActionResult? error = null) : IRequestValidator
{
    public Task<(T? Comando, IActionResult? Error)> ValidarAsync<T>(HttpRequest req, CancellationToken ct)
    {
        if (error is not null)
            return Task.FromResult<(T?, IActionResult?)>((default, error));
        if (comando is T resultado)
            return Task.FromResult<(T?, IActionResult?)>((resultado, null));
        return Task.FromResult<(T?, IActionResult?)>((default, null));
    }
}

internal class FakeSolicitudCommandRouter(Exception? lanzar = null, object? resultado = null) : ICommandRouter
{
    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (lanzar is not null) throw lanzar;
        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (lanzar is not null) throw lanzar;
        return Task.FromResult((TResult)(resultado ?? throw new NotImplementedException()));
    }
}
