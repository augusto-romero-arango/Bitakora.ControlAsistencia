using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction.CommandHandler;
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
        [new DateOnly(2026, 4, 7)]);

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    // Persiste SolicitudProgramacion antes de responder -> 201 Created sin Location (la
    // solicitud no tiene GET canonico; el turno diario en ControlHoras es un efecto posterior al
    // commit, no el recurso pedido).
    [Fact]
    public async Task SolicitarProgramacionTurno_Retorna201SinLocation_CuandoComandoEsValido()
    {
        var validator = new FakeSolicitudRequestValidator(ComandoValido());
        var router = new FakeSolicitudCommandRouter();
        var function = new FunctionEndpoint(validator, router);

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
    public async Task SolicitarProgramacionTurno_Retorna409_CuandoTodasLasFechasTienenAusencia()
    {
        var function = new FunctionEndpoint(
            new FakeSolicitudRequestValidator(ComandoValido()),
            new FakeSolicitudCommandRouter(new ReglaDeNegocioDeclinadaException(
                SolicitarProgramacionTurnoCommandHandler.Mensajes.TodasLasFechasConAusencia)));

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
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

    [Fact]
    public async Task SolicitarProgramacionTurno_Retorna409_CuandoSolicitudYaExiste()
    {
        var validator = new FakeSolicitudRequestValidator(ComandoValido());
        var router = new FakeSolicitudCommandRouter(
            lanzar: new RecursoYaExisteException(
                SolicitarProgramacionTurnoCommandHandler.Mensajes.SolicitudYaExiste));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_Retorna404_CuandoTurnoNoExisteEnElCatalogo()
    {
        var validator = new FakeSolicitudRequestValidator(ComandoValido());
        var router = new FakeSolicitudCommandRouter(
            lanzar: new RecursoNoEncontradoException(
                SolicitarProgramacionTurnoCommandHandler.Mensajes.TurnoNoEncontrado));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_Retorna409_CuandoElTurnoEstaRetirado()
    {
        var function = new FunctionEndpoint(
            new FakeSolicitudRequestValidator(ComandoValido()),
            new FakeSolicitudCommandRouter(new ReglaDeNegocioDeclinadaException(
                SolicitarProgramacionTurnoCommandHandler.Mensajes.TurnoRetirado)));

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>()
            .Which.Value.Should().Be(SolicitarProgramacionTurnoCommandHandler.Mensajes.TurnoRetirado);
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_PropagaInvalidOperationException_CuandoFallaLaInfraestructura()
    {
        var fallo = new InvalidOperationException("Fallo de infraestructura");
        var function = new FunctionEndpoint(
            new FakeSolicitudRequestValidator(ComandoValido()),
            new FakeSolicitudCommandRouter(fallo));

        var act = async () => await function.Run(FakeHttpRequest(), CancellationToken.None);

        (await act.Should().ThrowExactlyAsync<InvalidOperationException>()).Which.Should().BeSameAs(fallo);
    }
}

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

    private readonly object? _resultado;

    public FakeSolicitudCommandRouter(Exception? lanzar = null, object? resultado = null)
    {
        _excepcion = lanzar;
        _resultado = resultado;
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
    {
        if (_excepcion is not null)
            throw _excepcion;
        return Task.FromResult((TResult)(_resultado ?? throw new NotImplementedException()));
    }
}
