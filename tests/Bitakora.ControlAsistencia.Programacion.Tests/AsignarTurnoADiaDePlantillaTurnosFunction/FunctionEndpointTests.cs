using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AsignarTurnoADiaDePlantillaTurnosFunction;
using Bitakora.ControlAsistencia.Programacion.AsignarTurnoADiaDePlantillaTurnosFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AsignarTurnoADiaDePlantillaTurnosFunction;

public class FunctionEndpointTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000001281");
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000701");

    private static AsignarTurnoADiaDePlantillaTurnosBody BodyValido() => new(TurnoId);

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    private static HttpTriggerAttribute Trigger() =>
        typeof(FunctionEndpoint).GetMethod(nameof(FunctionEndpoint.Run))!.GetParameters()
            .Select(p => p.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(t => t is not null)!;

    private static FunctionEndpoint Crear(
        out FakeCommandRouterTurnos router, AsignarTurnoADiaDePlantillaTurnosBody? body = null,
        IActionResult? error = null, Exception? excepcion = null)
    {
        router = new FakeCommandRouterTurnos(excepcion);
        return new FunctionEndpoint(
            new FakeRequestValidatorTurnos<AsignarTurnoADiaDePlantillaTurnosBody>(body ?? BodyValido(), error),
            router);
    }

    [Fact]
    public void AsignarTurnoADiaDePlantillaTurnos_ExponeElVerboYLaRutaPactadosEnElIssue()
    {
        var trigger = Trigger();

        trigger.Methods.Should().Equal("put");
        trigger.Route.Should().Be("programacion/plantillas-turnos/{id}/dias/{dia}");
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaTurnos_Retorna204SinCuerpo_CuandoComandoEsValido()
    {
        var function = Crear(out var router);

        var result = await function.Run(FakeHttpRequest(), PlantillaId.ToString(), "3", CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        result.Should().NotBeAssignableTo<ObjectResult>();
        router.Invocado.Should().BeTrue();
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaTurnos_NuncaRetornaAcceptedResult_CuandoComandoEsValido()
    {
        var function = Crear(out var router);

        var result = await function.Run(FakeHttpRequest(), PlantillaId.ToString(), "3", CancellationToken.None);

        result.Should().NotBeOfType<AcceptedResult>();
        router.Invocado.Should().BeTrue();
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaTurnos_Retorna400SinInvocarElRouter_CuandoElIdNoEsUnGuid()
    {
        var function = Crear(out var router);

        var result = await function.Run(FakeHttpRequest(), "no-es-un-guid", "3", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.Invocado.Should().BeFalse();
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaTurnos_Retorna400SinInvocarElRouter_CuandoElDiaNoEsUnEntero()
    {
        var function = Crear(out var router);

        var result = await function.Run(FakeHttpRequest(), PlantillaId.ToString(), "x", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.Invocado.Should().BeFalse();
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaTurnos_Retorna400SinInvocarElRouter_CuandoElDiaEsCero()
    {
        var function = Crear(out var router);

        var result = await function.Run(FakeHttpRequest(), PlantillaId.ToString(), "0", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.Invocado.Should().BeFalse();
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaTurnos_Retorna400SinInvocarElRouter_CuandoElDiaEsNegativo()
    {
        var function = Crear(out var router);

        var result = await function.Run(FakeHttpRequest(), PlantillaId.ToString(), "-1", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.Invocado.Should().BeFalse();
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaTurnos_Retorna400SinInvocarElRouter_CuandoElBodyNoValida()
    {
        var function = Crear(out var router, error: new BadRequestObjectResult("El body es invalido"));

        var result = await function.Run(FakeHttpRequest(), PlantillaId.ToString(), "3", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.Invocado.Should().BeFalse();
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaTurnos_Retorna404_CuandoElRouterLanzaRecursoNoEncontradoException()
    {
        var function = Crear(out _, excepcion: new RecursoNoEncontradoException(
            AsignarTurnoADiaDePlantillaTurnosCommandHandler.Mensajes.PlantillaNoEncontrada));

        var result = await function.Run(FakeHttpRequest(), PlantillaId.ToString(), "3", CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaTurnos_Retorna409_CuandoElRouterLanzaReglaDeNegocioDeclinadaException()
    {
        var function = Crear(out _, excepcion: new ReglaDeNegocioDeclinadaException(
            AsignarTurnoADiaDePlantillaTurnosCommandHandler.Mensajes.DiaFueraDelCiclo));

        var result = await function.Run(FakeHttpRequest(), PlantillaId.ToString(), "3", CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaTurnos_PropagaInvalidOperationException_CuandoFallaLaInfraestructura()
    {
        var fallo = new InvalidOperationException("Fallo de infraestructura");
        var function = Crear(out _, excepcion: fallo);

        var act = async () => await function.Run(
            FakeHttpRequest(), PlantillaId.ToString(), "3", CancellationToken.None);

        (await act.Should().ThrowExactlyAsync<InvalidOperationException>()).Which.Should().BeSameAs(fallo);
    }
}

internal sealed class FakeRequestValidatorTurnos<TComando>(TComando? comando, IActionResult? error) : IRequestValidator
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

internal sealed class FakeCommandRouterTurnos(Exception? excepcion = null) : ICommandRouter
{
    public bool Invocado { get; private set; }

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        Invocado = true;
        if (excepcion is not null) throw excepcion;
        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default)
        where TCommand : class =>
        throw new NotImplementedException();
}
