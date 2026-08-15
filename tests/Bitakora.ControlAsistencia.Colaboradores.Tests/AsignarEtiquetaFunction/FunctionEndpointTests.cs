// Issue #376 (MEF-ADR-0043 paso 2): tests del endpoint HTTP PUT
// colaboradores/{id}/etiquetas/{categoria} (asignar o sobrescribir la etiqueta de una categoria).
// CA-1: 202; CA-3: id de ruta invalido -> 400 (parseo tipado unico, precedente
// ObtenerFichaColaborador); CA-ADR-0030 / MEF-ADR-0004: InvalidOperationException -> 409,
// KeyNotFoundException -> 404.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AsignarEtiquetaFunction;

public class FunctionEndpointTests
{
    private const string IdValido = "CC-79543210";
    private const string CategoriaValida = "Área";
    private const string ValorValido = "Tecnología";

    private static AsignarEtiquetaBody BodyValido() => new(ValorValido);

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    // CA-1: PUT exitoso retorna 202 Accepted
    [Fact]
    public async Task AsignarEtiqueta_Retorna202_CuandoIdDeRutaYBodySonValidos()
    {
        var validator = new FakeAsignarEtiquetaBodyRequestValidator(BodyValido());
        var router = new FakeAsignarEtiquetaCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CategoriaValida, CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-1: el endpoint compone el comando interno AsignarEtiqueta desde {id} + {categoria} + Valor
    // del body -- el router debe recibir exactamente esos 4 campos primitivos (MEF-ADR-0039
    // decision 6), tipo y numero derivados de Identificacion.Parsear.
    [Fact]
    public async Task AsignarEtiqueta_ComponeElComando_DesdeIdDeRutaCategoriaDeRutaYValorDelBody()
    {
        var validator = new FakeAsignarEtiquetaBodyRequestValidator(BodyValido());
        var router = new FakeAsignarEtiquetaCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        await function.Run(FakeHttpRequest(), IdValido, CategoriaValida, CancellationToken.None);

        router.ComandoRecibido.Should().Be(new AsignarEtiqueta(
            TipoIdentificacion: "CC",
            NumeroIdentificacion: "79543210",
            Categoria: CategoriaValida,
            Valor: ValorValido));
    }

    // CA-3: id de ruta sin guion -> 400, sin llegar a invocar el router (el parseo tipado es el
    // unico punto de traduccion, precedente ObtenerFichaColaborador.FunctionEndpoint).
    [Fact]
    public async Task AsignarEtiqueta_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var validator = new FakeAsignarEtiquetaBodyRequestValidator(BodyValido());
        var router = new FakeAsignarEtiquetaCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "CC79543210", CategoriaValida, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con un id invalido");
    }

    // CA-3: tipo fuera de la lista cerrada PILA -> 400.
    [Fact]
    public async Task AsignarEtiqueta_Retorna400_CuandoElTipoDeLaIdentificacionNoEstaEnLaListaCerrada()
    {
        var validator = new FakeAsignarEtiquetaBodyRequestValidator(BodyValido());
        var router = new FakeAsignarEtiquetaCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "XX-79543210", CategoriaValida, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // CA-1 (body invalido): Valor vacio en el body -> 400 Bad Request
    [Fact]
    public async Task AsignarEtiqueta_Retorna400_CuandoElBodyEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeAsignarEtiquetaBodyRequestValidator(error: errorDeValidacion);
        var router = new FakeAsignarEtiquetaCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CategoriaValida, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // CA-2: violacion de la regla de apertura (vinculacion con terminacion registrada) retorna 409
    // Conflict (MEF-ADR-0043 seccion 2 paso 2: el 409 de un PUT es una instancia mas de "declinar
    // con resultado").
    [Fact]
    public async Task AsignarEtiqueta_Retorna409_CuandoLaVinculacionTieneTerminacionRegistrada()
    {
        var validator = new FakeAsignarEtiquetaBodyRequestValidator(BodyValido());
        var router = new FakeAsignarEtiquetaCommandRouter(
            lanzar: new InvalidOperationException(
                "La vinculacion vigente del colaborador tiene una terminacion registrada"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CategoriaValida, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // CA-2: colaborador inexistente retorna 404 Not Found
    [Fact]
    public async Task AsignarEtiqueta_Retorna404_CuandoColaboradorNoExiste()
    {
        var validator = new FakeAsignarEtiquetaBodyRequestValidator(BodyValido());
        var router = new FakeAsignarEtiquetaCommandRouter(
            lanzar: new KeyNotFoundException("No existe un colaborador registrado con esa identificacion"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CategoriaValida, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}

// ---- Fakes manuales - NO NSubstitute ----

/// <summary>
/// Fake configurable de IRequestValidator para el body reducido (AsignarEtiquetaBody). Retorna un
/// body pre-configurado o un error segun lo que se le pase en el constructor.
/// </summary>
internal class FakeAsignarEtiquetaBodyRequestValidator : IRequestValidator
{
    private readonly AsignarEtiquetaBody? _body;
    private readonly IActionResult? _error;

    public FakeAsignarEtiquetaBodyRequestValidator(
        AsignarEtiquetaBody? body = null,
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

/// <summary>
/// Fake configurable de ICommandRouter. Registra el comando recibido (ComandoRecibido) para
/// verificar la composicion ruta+body, y puede completar exitosamente o lanzar la excepcion
/// configurada (InvalidOperationException -> 409, KeyNotFoundException -> 404).
/// </summary>
internal class FakeAsignarEtiquetaCommandRouter : ICommandRouter
{
    private readonly Exception? _excepcion;

    public AsignarEtiqueta? ComandoRecibido { get; private set; }

    public FakeAsignarEtiquetaCommandRouter(Exception? lanzar = null) =>
        _excepcion = lanzar;

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (command is AsignarEtiqueta asignarEtiqueta)
            ComandoRecibido = asignarEtiqueta;

        if (_excepcion is not null)
            throw _excepcion;

        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(
        TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
