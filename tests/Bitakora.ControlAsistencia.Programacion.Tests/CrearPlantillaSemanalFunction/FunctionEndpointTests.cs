using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearPlantillaSemanalFunction;

public class FunctionEndpointTests
{
    private static CrearPlantillaSemanal ComandoValido() =>
        new(Guid.NewGuid(), "Semana Cocina", 2);

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    private static MethodInfo Run() =>
        typeof(FunctionEndpoint).GetMethod(nameof(FunctionEndpoint.Run))!;

    private static HttpTriggerAttribute Trigger() =>
        Run().GetParameters()
            .Select(parametro => parametro.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(trigger => trigger is not null)!;

    // Ruta y verbo congelados por reflexion: Run() se invoca directo en los demas tests, sin pasar
    // por el enrutador del host, asi que nada mas ejercita el HttpTriggerAttribute.
    [Fact]
    public void CrearPlantillaSemanal_ExponeElVerboYLaRutaPactadosEnElIssue()
    {
        var trigger = Trigger();

        trigger.Methods.Should().Equal("post");
        trigger.Route.Should().Be("programacion/plantillas-semanales");
    }

    [Fact]
    public async Task CrearPlantillaSemanal_Retorna201ConLocation_CuandoComandoEsValido()
    {
        var comando = ComandoValido();
        var validator = new FakeRequestValidator<CrearPlantillaSemanal>(comando);
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        var creado = result.Should().BeOfType<CreatedResult>().Which;
        creado.Location.Should().Be($"/api/programacion/plantillas-semanales/{comando.PlantillaId}");
    }

    // Guarda contra alinear este endpoint con los del BC que aun devuelven 202 (ver #640).
    [Fact]
    public async Task CrearPlantillaSemanal_NuncaRetornaAcceptedResult_CuandoComandoEsValido()
    {
        var validator = new FakeRequestValidator<CrearPlantillaSemanal>(ComandoValido());
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().NotBeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task CrearPlantillaSemanal_Retorna409_CuandoPlantillaYaExiste()
    {
        var validator = new FakeRequestValidator<CrearPlantillaSemanal>(ComandoValido());
        var router = new FakeCommandRouter(lanzarInvalidOperationException: true);
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task CrearPlantillaSemanal_Retorna400_CuandoRequestEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeRequestValidator<CrearPlantillaSemanal>(error: errorDeValidacion);
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CrearPlantillaSemanal_Retorna400ConMensajes_CuandoElFactoryRechazaLosDatos()
    {
        var validator = new FakeRequestValidator<CrearPlantillaSemanal>(ComandoValido());
        var erroresDeNegocio = new ArgumentException[] { new("nombre vacio"), new("semanas fuera de rango") };
        var router = new FakeCommandRouter(erroresAggregateException: erroresDeNegocio);
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Which;
        badRequest.Value.Should().BeAssignableTo<IEnumerable<string>>()
            .Which.Should().BeEquivalentTo(erroresDeNegocio.Select(e => e.Message));
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

internal class FakeCommandRouter : ICommandRouter
{
    private readonly bool _lanzarInvalidOperation;
    private readonly ArgumentException[]? _erroresAggregate;

    public FakeCommandRouter(
        bool lanzarInvalidOperationException = false,
        ArgumentException[]? erroresAggregateException = null)
    {
        _lanzarInvalidOperation = lanzarInvalidOperationException;
        _erroresAggregate = erroresAggregateException;
    }

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (_lanzarInvalidOperation)
            throw new InvalidOperationException("La plantilla semanal ya existe");

        if (_erroresAggregate is not null)
            throw new AggregateException(_erroresAggregate);

        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(
        TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
