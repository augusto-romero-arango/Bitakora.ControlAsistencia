using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.QuitarFranjaFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.Tests.QuitarFranjaFunction;

public class FunctionEndpointTests
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000604");

    private static QuitarFranjaBody BodyValido() => new(new TimeOnly(15, 0));

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    private static MethodInfo Run() =>
        typeof(FunctionEndpoint).GetMethod(nameof(FunctionEndpoint.Run))!;

    private static HttpTriggerAttribute Trigger() =>
        Run().GetParameters()
            .Select(parametro => parametro.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(trigger => trigger is not null)!;

    // Congela verbo y ruta por reflexion: ningun otro test local ejercita el
    // HttpTriggerAttribute -- Run() se llama directo, sin pasar por el enrutador del host.
    [Fact]
    public void QuitarFranja_ExponeElVerboYLaRutaPactadosEnElIssue()
    {
        var trigger = Trigger();

        trigger.Methods.Should().Equal("post");
        trigger.Route.Should().Be("programacion/turnos/{id}:quitar-franja");
    }

    [Fact]
    public async Task QuitarFranja_Retorna202_CuandoComandoEsValido()
    {
        var validator = new FakeRequestValidator<QuitarFranjaBody>(BodyValido());
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), TurnoId.ToString(), CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task QuitarFranja_Retorna400_CuandoElIdDeRutaNoEsUnGuidValido()
    {
        var validator = new FakeRequestValidator<QuitarFranjaBody>(BodyValido());
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "no-es-un-guid", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task QuitarFranja_Retorna400_CuandoElBodyNoValida()
    {
        var error = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeRequestValidator<QuitarFranjaBody>(error: error);
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), TurnoId.ToString(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task QuitarFranja_Retorna404_CuandoElTurnoNoExiste()
    {
        var validator = new FakeRequestValidator<QuitarFranjaBody>(BodyValido());
        var router = new FakeCommandRouter(
            new KeyNotFoundException("No se encontro el turno con el Id especificado"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), TurnoId.ToString(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task QuitarFranja_Retorna409_CuandoLaFranjaNoExiste()
    {
        var validator = new FakeRequestValidator<QuitarFranjaBody>(BodyValido());
        var router = new FakeCommandRouter(
            new InvalidOperationException("Ninguna franja del turno empieza a la hora especificada"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), TurnoId.ToString(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
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

internal sealed class FakeCommandRouter(Exception? excepcion = null) : ICommandRouter
{
    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (excepcion is not null) throw excepcion;
        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default)
        where TCommand : class =>
        throw new NotImplementedException();
}
