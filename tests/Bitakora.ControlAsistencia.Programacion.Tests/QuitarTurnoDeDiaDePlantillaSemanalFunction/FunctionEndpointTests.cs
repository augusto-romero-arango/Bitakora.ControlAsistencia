// Issue #622: tests del endpoint HTTP DELETE
// programacion/plantillas-semanales/{id}/dias/{semana}/{dia}

using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.QuitarTurnoDeDiaDePlantillaSemanalFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.Tests.QuitarTurnoDeDiaDePlantillaSemanalFunction;

public class FunctionEndpointTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000000622");

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    private static MethodInfo Run() =>
        typeof(FunctionEndpoint).GetMethod(nameof(FunctionEndpoint.Run))!;

    private static HttpTriggerAttribute Trigger() =>
        Run().GetParameters()
            .Select(parametro => parametro.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(trigger => trigger is not null)!;

    [Fact]
    public void QuitarTurnoDeDiaDePlantillaSemanal_ExponeElVerboYLaRutaPactadosEnElIssue()
    {
        var trigger = Trigger();

        trigger.Methods.Should().Equal("delete");
        trigger.Route.Should().Be("programacion/plantillas-semanales/{id}/dias/{semana}/{dia}");
    }

    [Fact]
    public async Task QuitarTurnoDeDiaDePlantillaSemanal_Retorna204_CuandoComandoEsValido()
    {
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(
            FakeHttpRequest(), PlantillaId.ToString(), "1", "5", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    // El DELETE nunca es asincrono diferido -- ni siquiera cuando el dia ya estaba vacio.
    [Fact]
    public async Task QuitarTurnoDeDiaDePlantillaSemanal_NuncaRetornaAcceptedResult_CuandoComandoEsValido()
    {
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(
            FakeHttpRequest(), PlantillaId.ToString(), "1", "5", CancellationToken.None);

        result.Should().NotBeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task QuitarTurnoDeDiaDePlantillaSemanal_Retorna400_CuandoElIdDeRutaNoEsUnGuidValido()
    {
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(
            FakeHttpRequest(), "no-es-un-guid", "1", "5", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.Invocado.Should().BeFalse();
    }

    [Fact]
    public async Task QuitarTurnoDeDiaDePlantillaSemanal_Retorna400_CuandoLaSemanaNoEsUnEnteroValido()
    {
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(
            FakeHttpRequest(), PlantillaId.ToString(), "x", "5", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.Invocado.Should().BeFalse();
    }

    [Fact]
    public async Task QuitarTurnoDeDiaDePlantillaSemanal_Retorna400_CuandoLaSemanaEsCero()
    {
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(
            FakeHttpRequest(), PlantillaId.ToString(), "0", "5", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.Invocado.Should().BeFalse();
    }

    // {dia} fuera de 1..7 se traduce a 400 en el endpoint, ANTES de despachar (MEF-ADR-0004
    // capa 1): el router nunca se invoca.
    [Fact]
    public async Task QuitarTurnoDeDiaDePlantillaSemanal_Retorna400ConMensajeYSinInvocarElRouter_CuandoElDiaEsCero()
    {
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(
            FakeHttpRequest(), PlantillaId.ToString(), "1", "0", CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Which;
        badRequest.Value!.ToString().Should().Contain(DiaSemana.Mensajes.NumeroFueraDeRango);
        router.Invocado.Should().BeFalse();
    }

    [Fact]
    public async Task QuitarTurnoDeDiaDePlantillaSemanal_Retorna400ConMensajeYSinInvocarElRouter_CuandoElDiaEsOcho()
    {
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(
            FakeHttpRequest(), PlantillaId.ToString(), "1", "8", CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Which;
        badRequest.Value!.ToString().Should().Contain(DiaSemana.Mensajes.NumeroFueraDeRango);
        router.Invocado.Should().BeFalse();
    }

    [Fact]
    public async Task QuitarTurnoDeDiaDePlantillaSemanal_Retorna404_CuandoElRouterLanzaKeyNotFoundException()
    {
        var router = new FakeCommandRouter(
            new KeyNotFoundException("No se encontro la plantilla semanal con el Id especificado"));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(
            FakeHttpRequest(), PlantillaId.ToString(), "1", "5", CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task QuitarTurnoDeDiaDePlantillaSemanal_Retorna409_CuandoElRouterLanzaInvalidOperationException()
    {
        var router = new FakeCommandRouter(
            new InvalidOperationException("La semana especificada supera el numero de semanas de la plantilla"));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(
            FakeHttpRequest(), PlantillaId.ToString(), "1", "5", CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }
}

// Fake manual - NO NSubstitute.
internal sealed class FakeCommandRouter(Exception? excepcion = null) : ICommandRouter
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
