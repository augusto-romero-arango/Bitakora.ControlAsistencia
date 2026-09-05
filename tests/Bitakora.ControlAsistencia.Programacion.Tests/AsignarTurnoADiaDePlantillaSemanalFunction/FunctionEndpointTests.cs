using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AsignarTurnoADiaDePlantillaSemanalFunction;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AsignarTurnoADiaDePlantillaSemanalFunction;

public class FunctionEndpointTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000000621");
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000701");

    private static AsignarTurnoADiaDePlantillaSemanalBody BodyValido() => new(TurnoId);

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    private static MethodInfo Run() =>
        typeof(FunctionEndpoint).GetMethod(nameof(FunctionEndpoint.Run))!;

    private static HttpTriggerAttribute Trigger() =>
        Run().GetParameters()
            .Select(parametro => parametro.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(trigger => trigger is not null)!;

    // Los demas tests invocan Run() directo, sin pasar por el enrutador del host: solo este
    // congela la ruta y el verbo pactados, y por reflexion.
    [Fact]
    public void AsignarTurnoADiaDePlantillaSemanal_ExponeElVerboYLaRutaPactadosEnElIssue()
    {
        var trigger = Trigger();

        trigger.Methods.Should().Equal("put");
        trigger.Route.Should().Be("programacion/plantillas-semanales/{id}/dias/{semana}/{dia}");
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_Retorna204_CuandoComandoEsValido()
    {
        var validator = new FakeRequestValidator<AsignarTurnoADiaDePlantillaSemanalBody>(BodyValido());
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(
            FakeHttpRequest(), PlantillaId.ToString(), "1", "5", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    // El slot existe por construccion: el PUT nunca "crea" (RFC 9110 seccion 9.3.4).
    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_NuncaRetornaAcceptedResult_CuandoComandoEsValido()
    {
        var validator = new FakeRequestValidator<AsignarTurnoADiaDePlantillaSemanalBody>(BodyValido());
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(
            FakeHttpRequest(), PlantillaId.ToString(), "1", "5", CancellationToken.None);

        result.Should().NotBeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_Retorna400_CuandoElIdDeRutaNoEsUnGuidValido()
    {
        var validator = new FakeRequestValidator<AsignarTurnoADiaDePlantillaSemanalBody>(BodyValido());
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(
            FakeHttpRequest(), "no-es-un-guid", "1", "5", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.Invocado.Should().BeFalse();
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_Retorna400_CuandoLaSemanaNoEsUnEnteroValido()
    {
        var validator = new FakeRequestValidator<AsignarTurnoADiaDePlantillaSemanalBody>(BodyValido());
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(
            FakeHttpRequest(), PlantillaId.ToString(), "x", "5", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.Invocado.Should().BeFalse();
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_Retorna400_CuandoLaSemanaEsCero()
    {
        var validator = new FakeRequestValidator<AsignarTurnoADiaDePlantillaSemanalBody>(BodyValido());
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(
            FakeHttpRequest(), PlantillaId.ToString(), "0", "5", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.Invocado.Should().BeFalse();
    }

    // {dia} fuera de 1..7 se traduce a 400 en el endpoint, ANTES de despachar (MEF-ADR-0004
    // capa 1): el router nunca se invoca.
    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_Retorna400ConMensajeYSinInvocarElRouter_CuandoElDiaEsCero()
    {
        var validator = new FakeRequestValidator<AsignarTurnoADiaDePlantillaSemanalBody>(BodyValido());
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(
            FakeHttpRequest(), PlantillaId.ToString(), "1", "0", CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Which;
        badRequest.Value!.ToString().Should().Contain(DiaSemana.Mensajes.NumeroFueraDeRango);
        router.Invocado.Should().BeFalse();
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_Retorna400ConMensajeYSinInvocarElRouter_CuandoElDiaEsOcho()
    {
        var validator = new FakeRequestValidator<AsignarTurnoADiaDePlantillaSemanalBody>(BodyValido());
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(
            FakeHttpRequest(), PlantillaId.ToString(), "1", "8", CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Which;
        badRequest.Value!.ToString().Should().Contain(DiaSemana.Mensajes.NumeroFueraDeRango);
        router.Invocado.Should().BeFalse();
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_Retorna400_CuandoElBodyNoValida()
    {
        var error = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeRequestValidator<AsignarTurnoADiaDePlantillaSemanalBody>(error: error);
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(
            FakeHttpRequest(), PlantillaId.ToString(), "1", "5", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_Retorna404_CuandoElRouterLanzaKeyNotFoundException()
    {
        var validator = new FakeRequestValidator<AsignarTurnoADiaDePlantillaSemanalBody>(BodyValido());
        var router = new FakeCommandRouter(
            new KeyNotFoundException("No se encontro la plantilla semanal con el Id especificado"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(
            FakeHttpRequest(), PlantillaId.ToString(), "1", "5", CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantillaSemanal_Retorna409_CuandoElRouterLanzaInvalidOperationException()
    {
        var validator = new FakeRequestValidator<AsignarTurnoADiaDePlantillaSemanalBody>(BodyValido());
        var router = new FakeCommandRouter(
            new InvalidOperationException("El turno fue retirado del catalogo"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(
            FakeHttpRequest(), PlantillaId.ToString(), "1", "5", CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }
}

// Fakes manuales - NO NSubstitute.

internal sealed class FakeRequestValidator<TComando> : IRequestValidator
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
