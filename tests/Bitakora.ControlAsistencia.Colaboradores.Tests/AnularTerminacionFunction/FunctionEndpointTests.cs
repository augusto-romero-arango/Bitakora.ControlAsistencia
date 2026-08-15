// Issue #379 (MEF-ADR-0043 paso 4): tests del endpoint HTTP POST
// colaboradores/{id}/vinculaciones/{codigo}:anular-terminacion (anular la terminacion registrada
// de la ultima vinculacion de un colaborador, ahora direccionada por su codigo). {id} se parsea
// via Identificacion.Parsear (unico punto de conversion, MEF-ADR-0037); {codigo} viaja intacto al
// comando -- la comparacion contra el codigo vigente vive en el aggregate. SIN body: los tres
// campos del comando interno viajan completos en la ruta -- el endpoint NO depende de
// IRequestValidator (AnularTerminacionValidator se elimino junto con el body).
// CA-3: 202, con composicion exacta del comando interno desde {id} + {codigo}; CA-4/CA-5: reglas
// de estado y de codigo conservadas -> 409; CA-6: colaborador inexistente -> 404, {id} de ruta
// invalido -> 400 (precedente CorregirNombresFunction.FunctionEndpoint post-#377).
// Reemplaza el POST Colaboradores/Terminaciones/Anulaciones (issue #354): la ruta vieja deja de
// existir (CA-7, verificado por la ausencia de esa ruta en este archivo).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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

    // CA-3: POST exitoso retorna 202 Accepted
    [Fact]
    public async Task AnularTerminacion_Retorna202_CuandoIdDeRutaYCodigoSonValidos()
    {
        var router = new FakeAnularTerminacionCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CodigoValido, CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-3: el endpoint compone el comando interno AnularTerminacion desde {id} + {codigo} --
    // el router debe recibir exactamente esos 3 campos primitivos (MEF-ADR-0039 decision 6), tipo
    // y numero derivados de Identificacion.Parsear.
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

    // CA-6: id de ruta sin guion -> 400, sin llegar a invocar el router (el parseo tipado es el
    // unico punto de traduccion, precedente CorregirNombresFunction.FunctionEndpoint post-#377).
    [Fact]
    public async Task AnularTerminacion_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var router = new FakeAnularTerminacionCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), "CC79543210", CodigoValido, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con un id invalido");
    }

    // CA-6: tipo de identificacion fuera de la lista cerrada PILA -> 400.
    [Fact]
    public async Task AnularTerminacion_Retorna400_CuandoElTipoDeLaIdentificacionNoEstaEnLaListaCerrada()
    {
        var router = new FakeAnularTerminacionCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), "XX-79543210", CodigoValido, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con un id invalido");
    }

    // CA-6: numero vacio tras el guion del {id} de ruta -> 400.
    [Fact]
    public async Task AnularTerminacion_Retorna400_CuandoElNumeroDeLaIdentificacionQuedaVacio()
    {
        var router = new FakeAnularTerminacionCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), "CC-", CodigoValido, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con un id invalido");
    }

    // CA-4/CA-5: violacion de una regla de negocio (vinculacion abierta / codigo no corresponde)
    // retorna 409 Conflict.
    [Fact]
    public async Task AnularTerminacion_Retorna409_CuandoLaVinculacionEstaAbierta()
    {
        var router = new FakeAnularTerminacionCommandRouter(
            lanzar: new InvalidOperationException(
                "La vinculacion vigente del colaborador no tiene una terminacion registrada"));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CodigoValido, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // CA-6: colaborador inexistente retorna 404 Not Found
    [Fact]
    public async Task AnularTerminacion_Retorna404_CuandoColaboradorNoExiste()
    {
        var router = new FakeAnularTerminacionCommandRouter(
            lanzar: new KeyNotFoundException("No existe un colaborador registrado con esa identificacion"));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CodigoValido, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}

// ---- Fakes manuales - NO NSubstitute ----

/// <summary>
/// Fake configurable de ICommandRouter. Registra el comando recibido (ComandoRecibido) para
/// verificar la composicion desde la ruta, y puede completar exitosamente o lanzar la excepcion
/// configurada (InvalidOperationException -> 409, KeyNotFoundException -> 404).
/// </summary>
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
