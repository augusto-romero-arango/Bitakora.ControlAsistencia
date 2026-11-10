// {codigo} viaja intacto al comando: la comparacion contra el codigo vigente vive en el aggregate.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction;
using Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction.CommandHandler;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AnularTerminacionFunction;

public class FunctionEndpointTests
{
    private const string IdValido = "CC-79543210";
    private const string CodigoValido = "COL-001";

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    [Fact]
    public async Task AnularTerminacion_Retorna204SinCuerpo_CuandoIdDeRutaYCodigoSonValidos()
    {
        var router = new FakeAnularTerminacionCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CodigoValido, CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        result.Should().NotBeAssignableTo<ObjectResult>();
    }

    [Fact]
    public async Task AnularTerminacion_ComponeElComando_DesdeIdDeRutaYCodigo()
    {
        var router = new FakeAnularTerminacionCommandRouter();
        var function = new FunctionEndpoint(router);

        await function.Run(FakeHttpRequest(), IdValido, CodigoValido, CancellationToken.None);

        router.ComandoRecibido.Should().Be(new AnularTerminacion(
            TipoIdentificacion: "CC",
            NumeroIdentificacion: "79543210",
            Codigo: CodigoValido));
    }

    [Fact]
    public async Task AnularTerminacion_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var router = new FakeAnularTerminacionCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), "CC79543210", CodigoValido, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con un id invalido");
    }

    [Fact]
    public async Task AnularTerminacion_Retorna400_CuandoElTipoDeLaIdentificacionNoEstaEnLaListaCerrada()
    {
        var router = new FakeAnularTerminacionCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), "XX-79543210", CodigoValido, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con un id invalido");
    }

    [Fact]
    public async Task AnularTerminacion_Retorna400_CuandoElNumeroDeLaIdentificacionQuedaVacio()
    {
        var router = new FakeAnularTerminacionCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), "CC-", CodigoValido, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con un id invalido");
    }

    [Fact]
    public async Task AnularTerminacion_Retorna409_CuandoLaVinculacionEstaAbierta()
    {
        var router = new FakeAnularTerminacionCommandRouter(
            lanzar: new ReglaDeNegocioDeclinadaException(
                AnularTerminacionCommandHandler.Mensajes.VinculacionAbierta));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CodigoValido, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task AnularTerminacion_Retorna404_CuandoColaboradorNoExiste()
    {
        var router = new FakeAnularTerminacionCommandRouter(
            lanzar: new RecursoNoEncontradoException(
                AnularTerminacionCommandHandler.Mensajes.ColaboradorNoEncontrado));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CodigoValido, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AnularTerminacion_PropagaInvalidOperationException_CuandoFallaLaInfraestructura()
    {
        var function = new FunctionEndpoint(new FakeAnularTerminacionCommandRouter(
            new InvalidOperationException()));

        var act = async () => await function.Run(FakeHttpRequest(), IdValido, CodigoValido, CancellationToken.None);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>();
    }
}

internal class FakeAnularTerminacionCommandRouter : ICommandRouter
{
    private readonly Exception? _excepcion;

    public AnularTerminacion? ComandoRecibido { get; private set; }

    public FakeAnularTerminacionCommandRouter(Exception? lanzar = null) =>
        _excepcion = lanzar;

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (command is AnularTerminacion anularTerminacion)
            ComandoRecibido = anularTerminacion;

        if (_excepcion is not null)
            throw _excepcion;

        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(
        TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
