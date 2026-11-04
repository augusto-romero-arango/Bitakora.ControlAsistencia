using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.DescartarBorradorDeProgramacionFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.Programacion.Tests.DescartarBorradorDeProgramacionFunction;

public class FunctionEndpointTests
{
    private static DescartarBorradorDeProgramacionBody BodyValido() =>
        new(new DateOnly(2026, 10, 28), new DateOnly(2026, 11, 3));

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    [Fact]
    public async Task DescartarBorradorDeProgramacion_Retorna204SinCuerpo_CuandoElBodyEsValido()
    {
        var function = new FunctionEndpoint(
            new FakeDescartarRequestValidator(BodyValido()), new FakeDescartarCommandRouter());

        var result = await function.Run(FakeHttpRequest(), "E001", CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        result.Should().NotBeAssignableTo<ObjectResult>();
    }

    // Estado ya alcanzado: el router retorna normalmente (sin eventos ni mensajes) y el contrato sigue siendo 204.
    [Fact]
    public async Task DescartarBorradorDeProgramacion_Retorna204_CuandoNoHabiaNadaQueDescartar()
    {
        var router = new FakeDescartarCommandRouter();
        var function = new FunctionEndpoint(new FakeDescartarRequestValidator(BodyValido()), router);

        var result = await function.Run(FakeHttpRequest(), "E001", CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        router.Comandos.Should().HaveCount(1);
    }

    [Fact]
    public async Task DescartarBorradorDeProgramacion_DespachaElComandoConCodigoYRango_CuandoElBodyEsValido()
    {
        var router = new FakeDescartarCommandRouter();
        var function = new FunctionEndpoint(new FakeDescartarRequestValidator(BodyValido()), router);

        await function.Run(FakeHttpRequest(), "E001", CancellationToken.None);

        var comando = router.Comandos.Should().ContainSingle().Which.Should().BeOfType<DescartarBorradorDeProgramacion>().Subject;
        comando.CodigoColaborador.Should().Be("E001");
        comando.Desde.Should().Be(new DateOnly(2026, 10, 28));
        comando.Hasta.Should().Be(new DateOnly(2026, 11, 3));
    }

    [Fact]
    public async Task DescartarBorradorDeProgramacion_Retorna400SinTocarElDominio_CuandoElCodigoNoEsUrlSafe()
    {
        foreach (var codigo in new[] { "", "E 001", "E:001", "E/001", "COL\n" })
        {
            var router = new FakeDescartarCommandRouter();
            var function = new FunctionEndpoint(new FakeDescartarRequestValidator(BodyValido()), router);

            var result = await function.Run(FakeHttpRequest(), codigo, CancellationToken.None);

            result.Should().BeOfType<BadRequestObjectResult>($"el codigo '{codigo}' no es URL-safe")
                .Which.Value.Should().Be(
                    "El codigo del colaborador solo admite letras sin tilde, digitos y los caracteres - . _ ~");
            router.Comandos.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task DescartarBorradorDeProgramacion_Retorna400SinTocarElDominio_CuandoFallaLaValidacionDelBody()
    {
        var router = new FakeDescartarCommandRouter();
        var function = new FunctionEndpoint(
            new FakeDescartarRequestValidator(error: new BadRequestObjectResult("Rango invalido")), router);

        var result = await function.Run(FakeHttpRequest(), "E001", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.Comandos.Should().BeEmpty();
    }
}

internal sealed class FakeDescartarRequestValidator(
    DescartarBorradorDeProgramacionBody? body = null, IActionResult? error = null) : IRequestValidator
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

internal sealed class FakeDescartarCommandRouter : ICommandRouter
{
    public List<object> Comandos { get; } = [];

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        Comandos.Add(command);
        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
