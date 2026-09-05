// Issue #606: tests del endpoint HTTP POST /programacion/turnos/{id}:asignar-sede-franja

using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AsignarSedeAFranjaFunction;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AsignarSedeAFranjaFunction;

public class FunctionEndpointTests
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000606");
    private static readonly SedeProgramada Chapinero = new("SEDE-CHAPINERO", "Chapinero");

    private static AsignarSedeAFranjaBody BodyValido() => new(new TimeOnly(14, 0), Chapinero);

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    private static MethodInfo Run() =>
        typeof(FunctionEndpoint).GetMethod(nameof(FunctionEndpoint.Run))!;

    private static HttpTriggerAttribute Trigger() =>
        Run().GetParameters()
            .Select(parametro => parametro.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(trigger => trigger is not null)!;

    // CA-5: ruta y verbo pactados en el issue, congelados por reflexion -- ningun otro test local
    // ejercita el HttpTriggerAttribute (Run() se llama directo, sin pasar por el enrutador del host).
    [Fact]
    public void AsignarSedeAFranja_ExponeElVerboYLaRutaPactadosEnElIssue()
    {
        var trigger = Trigger();

        trigger.Methods.Should().Equal("post");
        trigger.Route.Should().Be("programacion/turnos/{id}:asignar-sede-franja");
    }

    [Fact]
    public async Task AsignarSedeAFranja_Retorna202_CuandoComandoEsValido()
    {
        var validator = new FakeRequestValidator<AsignarSedeAFranjaBody>(BodyValido());
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), TurnoId.ToString(), CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // El {id} de ruta se valida en el borde (MEF-ADR-0037 seccion 2): el comando nunca debe
    // despacharse con un id que no sea un Guid valido.
    [Fact]
    public async Task AsignarSedeAFranja_Retorna400_CuandoElIdDeRutaNoEsUnGuidValido()
    {
        var validator = new FakeRequestValidator<AsignarSedeAFranjaBody>(BodyValido());
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "no-es-un-guid", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AsignarSedeAFranja_Retorna400_CuandoElBodyNoValida()
    {
        var error = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeRequestValidator<AsignarSedeAFranjaBody>(error: error);
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), TurnoId.ToString(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // Invariante del VO (FranjaOrdinaria.Crear via ConSede): el handler deja subir la
    // ArgumentException sin envolverla, el endpoint la traduce a 400 con mensaje.
    [Fact]
    public async Task AsignarSedeAFranja_Retorna400ConMensaje_CuandoElRouterLanzaArgumentException()
    {
        var validator = new FakeRequestValidator<AsignarSedeAFranjaBody>(
            new AsignarSedeAFranjaBody(new TimeOnly(14, 0), new SedeProgramada("", "Suba")));
        var router = new FakeCommandRouter(
            new ArgumentException("La sede de la franja debe tener Id y Nombre no vacios"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), TurnoId.ToString(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AsignarSedeAFranja_Retorna404_CuandoElTurnoNoExiste()
    {
        var validator = new FakeRequestValidator<AsignarSedeAFranjaBody>(BodyValido());
        var router = new FakeCommandRouter(
            new KeyNotFoundException("No se encontro el turno con el Id especificado en el catalogo"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), TurnoId.ToString(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AsignarSedeAFranja_Retorna409_CuandoElRouterLanzaInvalidOperationException()
    {
        var validator = new FakeRequestValidator<AsignarSedeAFranjaBody>(BodyValido());
        var router = new FakeCommandRouter(
            new InvalidOperationException("Ninguna franja del turno empieza a la hora especificada"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), TurnoId.ToString(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // CA-5: el body sin la clave "sede" (retirar) se acepta y compone Sede = null -- el record no
    // exige la clave, Sede tiene default null.
    [Fact]
    public async Task AsignarSedeAFranja_ComponeSedeNull_CuandoElBodyNoTraeLaClaveSede()
    {
        var validator = new FakeRequestValidator<AsignarSedeAFranjaBody>(
            new AsignarSedeAFranjaBody(new TimeOnly(14, 0)));
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        await function.Run(FakeHttpRequest(), TurnoId.ToString(), CancellationToken.None);

        router.ComandoRecibido.Should().BeOfType<AsignarSedeAFranja>()
            .Which.Sede.Should().BeNull();
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

// ComandoRecibido: a diferencia del FakeCommandRouter de otros comandos de este dominio, este
// captura el comando despachado -- CA-5 exige verificar que el body sin la clave "sede" compone
// Sede = null, y ningun otro assert de este archivo puede verlo sin esa captura.
internal sealed class FakeCommandRouter(Exception? excepcion = null) : ICommandRouter
{
    public object? ComandoRecibido { get; private set; }

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        ComandoRecibido = command;
        if (excepcion is not null) throw excepcion;
        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default)
        where TCommand : class =>
        throw new NotImplementedException();
}
