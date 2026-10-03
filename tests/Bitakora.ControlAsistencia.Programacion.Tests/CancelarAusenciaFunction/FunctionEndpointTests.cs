using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CancelarAusenciaFunction;
using Bitakora.ControlAsistencia.Programacion.CancelarAusenciaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CancelarAusenciaFunction;

public class FunctionEndpointTests
{
    private static readonly Guid AusenciaId = Guid.Parse("019600a0-0000-7000-8000-000000000744");

    private static CancelarAusenciaBody BodyValido() =>
        new([new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 21)]);

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    [Fact]
    public async Task CancelarAusencia_Retorna204SinCuerpo_CuandoElBodyEsValido()
    {
        var function = new FunctionEndpoint(
            new FakeCancelarRequestValidator(BodyValido()), new FakeCancelarCommandRouter());

        var result = await function.Run(FakeHttpRequest(), "E001", AusenciaId.ToString(), CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        result.Should().NotBeAssignableTo<ObjectResult>();
    }

    // Estado ya alcanzado: el router retorna normalmente (sin eventos) y el contrato sigue siendo 204.
    [Fact]
    public async Task CancelarAusencia_Retorna204_CuandoElDominioNoTieneNadaQueCancelar()
    {
        var router = new FakeCancelarCommandRouter();
        var function = new FunctionEndpoint(new FakeCancelarRequestValidator(BodyValido()), router);

        var result = await function.Run(FakeHttpRequest(), "E001", AusenciaId.ToString(), CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        router.Comandos.Should().HaveCount(1);
    }

    [Fact]
    public async Task CancelarAusencia_DespachaElComandoConRutaYFechas_CuandoElBodyEsValido()
    {
        var router = new FakeCancelarCommandRouter();
        var function = new FunctionEndpoint(new FakeCancelarRequestValidator(BodyValido()), router);

        await function.Run(FakeHttpRequest(), "E001", AusenciaId.ToString(), CancellationToken.None);

        var comando = router.Comandos.Should().ContainSingle().Which.Should().BeOfType<CancelarAusencia>().Subject;
        comando.CodigoColaborador.Should().Be("E001");
        comando.AusenciaId.Should().Be(AusenciaId);
        comando.Fechas.Should().Equal(new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 21));
    }

    [Fact]
    public async Task CancelarAusencia_Retorna400SinTocarElDominio_CuandoElIdNoEsGuid()
    {
        var router = new FakeCancelarCommandRouter();
        var function = new FunctionEndpoint(new FakeCancelarRequestValidator(BodyValido()), router);

        var result = await function.Run(FakeHttpRequest(), "E001", "no-es-guid", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.Comandos.Should().BeEmpty();
    }

    [Fact]
    public async Task CancelarAusencia_Retorna400SinTocarElDominio_CuandoElCodigoNoEsUrlSafe()
    {
        foreach (var codigo in new[] { "", "E 001", "E:001", "E/001", "COL\n" })
        {
            var router = new FakeCancelarCommandRouter();
            var function = new FunctionEndpoint(new FakeCancelarRequestValidator(BodyValido()), router);

            var result = await function.Run(FakeHttpRequest(), codigo, AusenciaId.ToString(), CancellationToken.None);

            result.Should().BeOfType<BadRequestObjectResult>($"el codigo '{codigo}' no es URL-safe");
            router.Comandos.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task CancelarAusencia_Retorna400_CuandoFallaLaValidacionDelBody()
    {
        var router = new FakeCancelarCommandRouter();
        var function = new FunctionEndpoint(
            new FakeCancelarRequestValidator(error: new BadRequestObjectResult("Fechas requeridas")), router);

        var result = await function.Run(FakeHttpRequest(), "E001", AusenciaId.ToString(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.Comandos.Should().BeEmpty();
    }

    [Fact]
    public async Task CancelarAusencia_Retorna404_CuandoLaAusenciaNoExiste()
    {
        var mensaje = CancelarAusenciaCommandHandler.Mensajes.AusenciaNoEncontrada;
        var function = new FunctionEndpoint(
            new FakeCancelarRequestValidator(BodyValido()),
            new FakeCancelarCommandRouter(new RecursoNoEncontradoException(mensaje)));

        var result = await function.Run(FakeHttpRequest(), "E001", AusenciaId.ToString(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>().Which.Value.Should().Be(mensaje);
    }

    [Fact]
    public async Task CancelarAusencia_PropagaInvalidOperationException_CuandoFallaLaInfraestructura()
    {
        var fallo = new InvalidOperationException("Fallo de infraestructura");
        var function = new FunctionEndpoint(
            new FakeCancelarRequestValidator(BodyValido()), new FakeCancelarCommandRouter(fallo));

        var act = async () => await function.Run(
            FakeHttpRequest(), "E001", AusenciaId.ToString(), CancellationToken.None);

        (await act.Should().ThrowExactlyAsync<InvalidOperationException>()).Which.Should().BeSameAs(fallo);
    }
}

internal sealed class FakeCancelarRequestValidator(
    CancelarAusenciaBody? body = null, IActionResult? error = null) : IRequestValidator
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

internal sealed class FakeCancelarCommandRouter(Exception? excepcion = null) : ICommandRouter
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
