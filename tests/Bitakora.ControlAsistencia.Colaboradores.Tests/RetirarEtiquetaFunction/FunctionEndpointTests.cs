// Retirar una categoria sin etiqueta es un no-op exitoso.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.RetirarEtiquetaFunction;
using Bitakora.ControlAsistencia.Colaboradores.RetirarEtiquetaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.RetirarEtiquetaFunction;

public class FunctionEndpointTests
{
    private const string IdValido = "CC-79543210";
    private const string CategoriaValida = "Área";

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    [Fact]
    public async Task RetirarEtiqueta_Retorna204SinCuerpo_CuandoIdDeRutaEsValido()
    {
        var router = new FakeRetirarEtiquetaCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CategoriaValida, CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        result.Should().NotBeAssignableTo<ObjectResult>();
    }

    [Fact]
    public async Task RetirarEtiqueta_ComponeElComando_DesdeIdDeRutaYCategoriaDeRutaSinBody()
    {
        var router = new FakeRetirarEtiquetaCommandRouter();
        var function = new FunctionEndpoint(router);

        await function.Run(FakeHttpRequest(), IdValido, CategoriaValida, CancellationToken.None);

        router.ComandoRecibido.Should().Be(new RetirarEtiqueta(
            TipoIdentificacion: "CC",
            NumeroIdentificacion: "79543210",
            Categoria: CategoriaValida));
    }

    [Fact]
    public async Task RetirarEtiqueta_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var router = new FakeRetirarEtiquetaCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), "CC79543210", CategoriaValida, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con un id invalido");
    }

    [Fact]
    public async Task RetirarEtiqueta_Retorna400_CuandoElTipoDeLaIdentificacionNoEstaEnLaListaCerrada()
    {
        var router = new FakeRetirarEtiquetaCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), "XX-79543210", CategoriaValida, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // {categoria} llega cruda de la ruta y, sin body, ningun validator la ve: un segmento en blanco
    // ("%20") hace match con la plantilla y, sin guarda de borde, el ArgumentException de
    // Etiqueta.NormalizarCategoria saldria como 500.
    [Fact]
    public async Task RetirarEtiqueta_Retorna400_CuandoLaCategoriaDeRutaEstaEnBlanco()
    {
        var router = new FakeRetirarEtiquetaCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), IdValido, "   ", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull(
            "el router nunca deberia invocarse con una categoria en blanco");
    }

    [Fact]
    public async Task RetirarEtiqueta_Retorna409_CuandoLaVinculacionEstaTerminada()
    {
        var router = new FakeRetirarEtiquetaCommandRouter(
            lanzar: new ReglaDeNegocioDeclinadaException(
                RetirarEtiquetaCommandHandler.Mensajes.VinculacionTerminada));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CategoriaValida, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task RetirarEtiqueta_Retorna404_CuandoColaboradorNoExiste()
    {
        var router = new FakeRetirarEtiquetaCommandRouter(
            lanzar: new RecursoNoEncontradoException(
                RetirarEtiquetaCommandHandler.Mensajes.ColaboradorNoEncontrado));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CategoriaValida, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RetirarEtiqueta_PropagaInvalidOperationException_CuandoFallaLaInfraestructura()
    {
        var function = new FunctionEndpoint(new FakeRetirarEtiquetaCommandRouter(
            new InvalidOperationException()));

        var act = async () => await function.Run(FakeHttpRequest(), IdValido, CategoriaValida, CancellationToken.None);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>();
    }
}

internal class FakeRetirarEtiquetaCommandRouter : ICommandRouter
{
    private readonly Exception? _excepcion;

    public RetirarEtiqueta? ComandoRecibido { get; private set; }

    public FakeRetirarEtiquetaCommandRouter(Exception? lanzar = null) =>
        _excepcion = lanzar;

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (command is RetirarEtiqueta retirarEtiqueta)
            ComandoRecibido = retirarEtiqueta;

        if (_excepcion is not null)
            throw _excepcion;

        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(
        TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
