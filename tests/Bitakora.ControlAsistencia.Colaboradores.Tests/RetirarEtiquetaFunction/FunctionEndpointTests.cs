// Issue #376 (MEF-ADR-0043 paso 3): tests del endpoint HTTP DELETE
// colaboradores/{id}/etiquetas/{categoria} (retirar la etiqueta de una categoria, sin body).
// CA-2: 202; CA-3: id de ruta invalido -> 400 (parseo tipado unico, precedente
// ObtenerFichaColaborador); CA-ADR-0030 / MEF-ADR-0004: InvalidOperationException -> 409,
// KeyNotFoundException -> 404.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.RetirarEtiquetaFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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

    // CA-2: DELETE exitoso retorna 202 Accepted
    [Fact]
    public async Task RetirarEtiqueta_Retorna202_CuandoIdDeRutaEsValido()
    {
        var router = new FakeRetirarEtiquetaCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CategoriaValida, CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-2: el endpoint compone el comando interno RetirarEtiqueta ENTERAMENTE desde la ruta ({id} +
    // {categoria}), sin body -- el router debe recibir esos 3 campos primitivos (MEF-ADR-0039
    // decision 6), tipo y numero derivados de Identificacion.Parsear.
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

    // CA-3: id de ruta sin guion -> 400, sin llegar a invocar el router (el parseo tipado es el
    // unico punto de traduccion, precedente ObtenerFichaColaborador.FunctionEndpoint).
    [Fact]
    public async Task RetirarEtiqueta_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var router = new FakeRetirarEtiquetaCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), "CC79543210", CategoriaValida, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con un id invalido");
    }

    // CA-3: tipo fuera de la lista cerrada PILA -> 400.
    [Fact]
    public async Task RetirarEtiqueta_Retorna400_CuandoElTipoDeLaIdentificacionNoEstaEnLaListaCerrada()
    {
        var router = new FakeRetirarEtiquetaCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), "XX-79543210", CategoriaValida, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // CA-2 (rutas de rechazo): categoria inexistente o vinculacion con terminacion registrada
    // retorna 409 Conflict.
    [Fact]
    public async Task RetirarEtiqueta_Retorna409_CuandoLaCategoriaNoExisteOLaVinculacionEstaTerminada()
    {
        var router = new FakeRetirarEtiquetaCommandRouter(
            lanzar: new InvalidOperationException("No existe una etiqueta asignada con esa categoria"));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CategoriaValida, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // CA-2: colaborador inexistente retorna 404 Not Found
    [Fact]
    public async Task RetirarEtiqueta_Retorna404_CuandoColaboradorNoExiste()
    {
        var router = new FakeRetirarEtiquetaCommandRouter(
            lanzar: new KeyNotFoundException("No existe un colaborador registrado con esa identificacion"));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CategoriaValida, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}

// ---- Fakes manuales - NO NSubstitute ----

/// <summary>
/// Fake configurable de ICommandRouter. Registra el comando recibido (ComandoRecibido) para
/// verificar la composicion integramente desde la ruta, y puede completar exitosamente o lanzar la
/// excepcion configurada (InvalidOperationException -> 409, KeyNotFoundException -> 404).
/// </summary>
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
