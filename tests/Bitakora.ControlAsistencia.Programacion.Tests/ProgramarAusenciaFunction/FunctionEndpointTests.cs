using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.ProgramarAusenciaFunction;
using Bitakora.ControlAsistencia.Programacion.ProgramarAusenciaFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ProgramarAusenciaFunction;

public class FunctionEndpointTests
{
    private static readonly Guid AusenciaId = Guid.Parse("019600a0-0000-7000-8000-000000000743");

    private static ProgramarAusenciaBody BodyValido() => new(
        AusenciaId, "CC-12345678", "Ana Maria Gomez",
        new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 7), "Vacaciones");

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    // POST crea una entidad con identidad propia -> 201 sin Location: la lectura de ausencias aun
    // no existe (MEF-ADR-0043 paso 1; CA-ADR-0035).
    [Fact]
    public async Task ProgramarAusencia_Retorna201SinLocation_CuandoElBodyEsValido()
    {
        var function = new FunctionEndpoint(
            new FakeAusenciaRequestValidator(BodyValido()), new FakeAusenciaCommandRouter());

        var result = await function.Run(FakeHttpRequest(), "E001", CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
        result.Should().BeAssignableTo<CreatedResult>().Which.Location.Should().BeNull();
    }

    [Fact]
    public async Task ProgramarAusencia_DespachaElComandoConElCodigoDeLaRuta_CuandoElBodyEsValido()
    {
        var router = new FakeAusenciaCommandRouter();
        var function = new FunctionEndpoint(new FakeAusenciaRequestValidator(BodyValido()), router);

        await function.Run(FakeHttpRequest(), "E001", CancellationToken.None);

        router.Comandos.Should().ContainSingle().Which.Should().Be(new ProgramarAusencia(
            AusenciaId, "E001", "CC-12345678", "Ana Maria Gomez",
            new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 7), "Vacaciones"));
    }

    [Fact]
    public async Task ProgramarAusencia_Retorna400_CuandoFallaLaValidacionDelBody()
    {
        var router = new FakeAusenciaCommandRouter();
        var function = new FunctionEndpoint(
            new FakeAusenciaRequestValidator(error: new BadRequestObjectResult("Campos requeridos faltantes")),
            router);

        var result = await function.Run(FakeHttpRequest(), "E001", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.Comandos.Should().BeEmpty();
    }

    [Fact]
    public async Task ProgramarAusencia_Retorna400SinTocarElDominio_CuandoElCodigoNoEsUrlSafe()
    {
        foreach (var codigo in new[] { "", "E 001", "E:001", "E/001", "COL\n" })
        {
            var router = new FakeAusenciaCommandRouter();
            var function = new FunctionEndpoint(new FakeAusenciaRequestValidator(BodyValido()), router);

            var result = await function.Run(FakeHttpRequest(), codigo, CancellationToken.None);

            result.Should().BeOfType<BadRequestObjectResult>($"el codigo '{codigo}' no es URL-safe");
            router.Comandos.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task ProgramarAusencia_Retorna409_CuandoLaAusenciaChocaConUnaVigente()
    {
        var mensaje = ProgramarAusenciaCommandHandler.Mensajes.ChoqueConAusencia(
            [new DateOnly(2026, 10, 5)], Programacion.DomainEvents.MotivoAusencia.Vacaciones);
        var function = new FunctionEndpoint(
            new FakeAusenciaRequestValidator(BodyValido()),
            new FakeAusenciaCommandRouter(new ReglaDeNegocioDeclinadaException(mensaje)));

        var result = await function.Run(FakeHttpRequest(), "E001", CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>().Which.Value.Should().Be(mensaje);
    }

    [Fact]
    public async Task ProgramarAusencia_Retorna409_CuandoElIdYaExiste()
    {
        var function = new FunctionEndpoint(
            new FakeAusenciaRequestValidator(BodyValido()),
            new FakeAusenciaCommandRouter(
                new RecursoYaExisteException(ProgramarAusenciaCommandHandler.Mensajes.AusenciaYaExiste)));

        var result = await function.Run(FakeHttpRequest(), "E001", CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task ProgramarAusencia_PropagaInvalidOperationException_CuandoFallaLaInfraestructura()
    {
        var fallo = new InvalidOperationException("Fallo de infraestructura");
        var function = new FunctionEndpoint(
            new FakeAusenciaRequestValidator(BodyValido()), new FakeAusenciaCommandRouter(fallo));

        var act = async () => await function.Run(FakeHttpRequest(), "E001", CancellationToken.None);

        (await act.Should().ThrowExactlyAsync<InvalidOperationException>()).Which.Should().BeSameAs(fallo);
    }
}

internal sealed class FakeAusenciaRequestValidator(
    ProgramarAusenciaBody? body = null, IActionResult? error = null) : IRequestValidator
{
    public Task<(T? Comando, IActionResult? Error)> ValidarAsync<T>(HttpRequest req, CancellationToken ct)
    {
        if (error is not null)
            return Task.FromResult<(T?, IActionResult?)>((default, error));

        if (body is T resultado)
            return Task.FromResult<(T?, IActionResult?)>((resultado, null));

        return Task.FromResult<(T?, IActionResult?)>((default, null));
    }
}

internal sealed class FakeAusenciaCommandRouter(Exception? excepcion = null) : ICommandRouter
{
    public List<object> Comandos { get; } = [];

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        Comandos.Add(command);
        if (excepcion is not null) throw excepcion;
        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
