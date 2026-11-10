using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Bitakora.ControlAsistencia.Colaboradores.RegistrarColaboradorFunction;
using Bitakora.ControlAsistencia.Colaboradores.RegistrarColaboradorFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.RegistrarColaboradorFunction;

public class FunctionEndpointTests
{
    private static RegistrarColaborador ComandoValido() => new(
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

    // Location se compone via el VO Identificacion, no concatenando strings.
    [Fact]
    public async Task RegistrarColaborador_Retorna201ConLocation_CuandoComandoEsValido()
    {
        var validator = new FakeRequestValidator<RegistrarColaborador>(ComandoValido());
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);

        var identificacion = Identificacion.Crear(TipoIdentificacion.Desde("CC"), "79543210");
        result.As<CreatedResult>().Location.Should().Be($"/api/colaboradores/fichas/{identificacion}");
    }

    [Fact]
    public async Task RegistrarColaborador_Retorna409_CuandoIdentificacionYaExiste()
    {
        var validator = new FakeRequestValidator<RegistrarColaborador>(ComandoValido());
        var router = new FakeCommandRouter(new RecursoYaExisteException(
            RegistrarColaboradorCommandHandler.Mensajes.ColaboradorYaRegistrado));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task RegistrarColaborador_PropagaInvalidOperationException_CuandoFallaLaInfraestructura()
    {
        var function = new FunctionEndpoint(
            new FakeRequestValidator<RegistrarColaborador>(ComandoValido()),
            new FakeCommandRouter(new InvalidOperationException()));

        var act = async () => await function.Run(FakeHttpRequest(), CancellationToken.None);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RegistrarColaborador_Retorna400_CuandoRequestEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeRequestValidator<RegistrarColaborador>(error: errorDeValidacion);
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}

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
    private readonly Exception? _excepcion;

    public FakeCommandRouter(Exception? lanzar = null) => _excepcion = lanzar;

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
