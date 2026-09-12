// PUT porque reemplaza completo un valor atomico direccionable (MEF-ADR-0043 paso 2): asignar y
// reasignar son el mismo reemplazo. El codigo de sede viaja en el body, no como segmento de ruta --
// es un dato de tercero sin invariante URL-safe propia (MEF-ADR-0043 seccion 1.2).
// {id} se parsea UNA vez via IdentificacionDeRuta.TryParsear, con 400 explicito si falla
// (MEF-ADR-0037 seccion 2).
// Issue #659 (MEF-ADR-0004 "Respuestas HTTP" enmendado por harness#849, MEF-ADR-0043 seccion 2 paso
// 2: PUT reemplaza -> 204 siempre): el slot de sede existe por construccion, vacio o lleno -- PUT
// lo reemplaza, nunca lo crea. La sede identica a la vigente (SinCambios) y el slot vacio responden
// ambos 204, nunca 201.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.AsignarSedeFunction;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AsignarSedeFunction;

public class FunctionEndpointTests
{
    private const string IdValido = "CC-79543210";
    private const string CodigoSedeValido = "BOG";

    private static AsignarSedeBody BodyValido() => new(CodigoSedeValido);

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    [Fact]
    public async Task AsignarSede_Retorna204SinCuerpo_CuandoIdDeRutaYBodySonValidos()
    {
        var validator = new FakeAsignarSedeBodyRequestValidator(BodyValido());
        var router = new FakeAsignarSedeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        result.Should().NotBeAssignableTo<ObjectResult>();
    }

    // Estado ya alcanzado (MEF-ADR-0004, "PUT reemplaza"): la sede solicitada ya es la vigente, o
    // el slot esta vacio y PUT lo llena por primera vez -- en ambos casos el slot existe por
    // construccion y el router retorna normalmente (SinCambios sin evento, o el reemplazo mismo).
    // El endpoint sigue respondiendo 204, nunca 201/404/409.
    [Fact]
    public async Task AsignarSede_Retorna204SinCuerpo_CuandoLaSedeYaEsLaVigente()
    {
        var validator = new FakeAsignarSedeBodyRequestValidator(BodyValido());
        var router = new FakeAsignarSedeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        result.Should().NotBeAssignableTo<ObjectResult>();
    }

    [Fact]
    public async Task AsignarSede_ComponeElComando_DesdeIdDeRutaYCodigoSedeDelBody()
    {
        var validator = new FakeAsignarSedeBodyRequestValidator(BodyValido());
        var router = new FakeAsignarSedeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        router.ComandoRecibido.Should().Be(new AsignarSede(
            TipoIdentificacion: "CC",
            NumeroIdentificacion: "79543210",
            CodigoSede: CodigoSedeValido));
    }

    // El parseo del {id} corta antes de despachar: un id invalido nunca llega al router.
    [Fact]
    public async Task AsignarSede_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var validator = new FakeAsignarSedeBodyRequestValidator(BodyValido());
        var router = new FakeAsignarSedeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "CC79543210", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con un id invalido");
    }

    [Fact]
    public async Task AsignarSede_Retorna400_CuandoElTipoDeLaIdentificacionNoEstaEnLaListaCerrada()
    {
        var validator = new FakeAsignarSedeBodyRequestValidator(BodyValido());
        var router = new FakeAsignarSedeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "XX-79543210", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AsignarSede_Retorna400_CuandoElBodyEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeAsignarSedeBodyRequestValidator(error: errorDeValidacion);
        var router = new FakeAsignarSedeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AsignarSede_Retorna409_CuandoLaVinculacionTieneTerminacionRegistrada()
    {
        var validator = new FakeAsignarSedeBodyRequestValidator(BodyValido());
        var router = new FakeAsignarSedeCommandRouter(
            lanzar: new InvalidOperationException(
                "La vinculacion vigente del colaborador tiene una terminacion registrada"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task AsignarSede_Retorna404_CuandoColaboradorNoExiste()
    {
        var validator = new FakeAsignarSedeBodyRequestValidator(BodyValido());
        var router = new FakeAsignarSedeCommandRouter(
            lanzar: new KeyNotFoundException("No existe un colaborador registrado con esa identificacion"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}

internal class FakeAsignarSedeBodyRequestValidator : IRequestValidator
{
    private readonly AsignarSedeBody? _body;
    private readonly IActionResult? _error;

    public FakeAsignarSedeBodyRequestValidator(
        AsignarSedeBody? body = null,
        IActionResult? error = null)
    {
        _body = body;
        _error = error;
    }

    public Task<(T? Comando, IActionResult? Error)> ValidarAsync<T>(
        HttpRequest req, CancellationToken ct)
    {
        if (_error is not null)
            return Task.FromResult<(T?, IActionResult?)>((default, _error));

        if (_body is T resultado)
            return Task.FromResult<(T?, IActionResult?)>((resultado, null));

        return Task.FromResult<(T?, IActionResult?)>((default, null));
    }
}

internal class FakeAsignarSedeCommandRouter : ICommandRouter
{
    private readonly Exception? _excepcion;

    public AsignarSede? ComandoRecibido { get; private set; }

    public FakeAsignarSedeCommandRouter(Exception? lanzar = null) =>
        _excepcion = lanzar;

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (command is AsignarSede asignarSede)
            ComandoRecibido = asignarSede;

        if (_excepcion is not null)
            throw _excepcion;

        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(
        TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
