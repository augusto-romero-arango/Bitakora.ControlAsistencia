using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction;
using Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

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

    // Tambien cuando la etiqueta ya es la vigente (SinCambios): el endpoint no distingue ambos caminos;
    // el no-op sin evento lo cubre AsignarEtiquetaCommandHandlerTests.
    [Fact]
    public async Task AsignarEtiqueta_Retorna204SinCuerpo_CuandoIdDeRutaYBodySonValidos()
    {
        var validator = new FakeAsignarEtiquetaBodyRequestValidator(BodyValido());
        var router = new FakeAsignarEtiquetaCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CategoriaValida, CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        result.Should().NotBeAssignableTo<ObjectResult>();
    }

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

    [Fact]
    public async Task AsignarEtiqueta_Retorna400_CuandoElTipoDeLaIdentificacionNoEstaEnLaListaCerrada()
    {
        var validator = new FakeAsignarEtiquetaBodyRequestValidator(BodyValido());
        var router = new FakeAsignarEtiquetaCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "XX-79543210", CategoriaValida, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // {categoria} llega cruda de la ruta y ningun validator la ve: un segmento en blanco ("%20") hace
    // match con la plantilla y, sin guarda de borde, el ArgumentException de Etiqueta.Crear saldria como 500.
    [Fact]
    public async Task AsignarEtiqueta_Retorna400_CuandoLaCategoriaDeRutaEstaEnBlanco()
    {
        var validator = new FakeAsignarEtiquetaBodyRequestValidator(BodyValido());
        var router = new FakeAsignarEtiquetaCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, "   ", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull(
            "el router nunca deberia invocarse con una categoria en blanco");
    }

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

    [Fact]
    public async Task AsignarEtiqueta_Retorna409_CuandoLaVinculacionTieneTerminacionRegistrada()
    {
        var validator = new FakeAsignarEtiquetaBodyRequestValidator(BodyValido());
        var router = new FakeAsignarEtiquetaCommandRouter(
            lanzar: new ReglaDeNegocioDeclinadaException(
                AsignarEtiquetaCommandHandler.Mensajes.VinculacionTerminada));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CategoriaValida, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task AsignarEtiqueta_Retorna404_CuandoColaboradorNoExiste()
    {
        var validator = new FakeAsignarEtiquetaBodyRequestValidator(BodyValido());
        var router = new FakeAsignarEtiquetaCommandRouter(
            lanzar: new RecursoNoEncontradoException(
                AsignarEtiquetaCommandHandler.Mensajes.ColaboradorNoEncontrado));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CategoriaValida, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AsignarEtiqueta_PropagaInvalidOperationException_CuandoFallaLaInfraestructura()
    {
        var function = new FunctionEndpoint(
            new FakeAsignarEtiquetaBodyRequestValidator(BodyValido()),
            new FakeAsignarEtiquetaCommandRouter(new InvalidOperationException()));

        var act = async () => await function.Run(FakeHttpRequest(), IdValido, CategoriaValida, CancellationToken.None);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>();
    }
}

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
