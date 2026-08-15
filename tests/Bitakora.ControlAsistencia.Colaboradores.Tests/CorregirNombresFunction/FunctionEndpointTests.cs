// Issue #377 (MEF-ADR-0043 paso 2): tests del endpoint HTTP PUT colaboradores/{id}/nombres
// (corregir los nombres de un colaborador existente -- reemplazo completo del VO atomico
// NombreColaborador, direccionable por {id}). Reemplaza el POST Colaboradores/Nombres (issue #351):
// la ruta vieja deja de existir (CA-5, verificado por la ausencia de esta ruta en este archivo).
// CA-1: 202, con composicion exacta del comando interno CorregirNombres desde {id} + body; CA-2:
// colaborador inexistente -> 404 (sin 409: este comando no tiene reglas de estado, CA-ADR-0030);
// CA-3: id de ruta invalido -> 400 (parseo tipado unico, precedente
// AsignarEtiquetaFunction.FunctionEndpoint post-#376); CA-4: body invalido (PrimerNombre/
// PrimerApellido vacios) -> 400.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.CorregirNombresFunction;

public class FunctionEndpointTests
{
    private const string IdValido = "CC-79543210";

    private static CorregirNombresBody BodyValido() => new(
        PrimerNombre: "Luis",
        SegundoNombre: "Augusto",
        PrimerApellido: "Barreto",
        SegundoApellido: null);

    private static HttpRequest FakeHttpRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    // CA-1: PUT exitoso retorna 202 Accepted
    [Fact]
    public async Task CorregirNombres_Retorna202_CuandoIdDeRutaYBodySonValidos()
    {
        var validator = new FakeCorregirNombresBodyRequestValidator(BodyValido());
        var router = new FakeCorregirNombresCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-1: el endpoint compone el comando interno CorregirNombres desde {id} + los 4 campos del
    // body -- el router debe recibir exactamente esos 6 campos primitivos (MEF-ADR-0039 decision
    // 6), tipo y numero derivados de Identificacion.Parsear.
    [Fact]
    public async Task CorregirNombres_ComponeElComando_DesdeIdDeRutaYBody()
    {
        var validator = new FakeCorregirNombresBodyRequestValidator(BodyValido());
        var router = new FakeCorregirNombresCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        router.ComandoRecibido.Should().Be(new CorregirNombres(
            TipoIdentificacion: "CC",
            NumeroIdentificacion: "79543210",
            PrimerNombre: "Luis",
            SegundoNombre: "Augusto",
            PrimerApellido: "Barreto",
            SegundoApellido: null));
    }

    // CA-3: id de ruta sin guion -> 400, sin llegar a invocar el router (el parseo tipado es el
    // unico punto de traduccion, precedente AsignarEtiquetaFunction.FunctionEndpoint post-#376).
    [Fact]
    public async Task CorregirNombres_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var validator = new FakeCorregirNombresBodyRequestValidator(BodyValido());
        var router = new FakeCorregirNombresCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "CC79543210", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con un id invalido");
    }

    // CA-3: tipo de identificacion fuera de la lista cerrada PILA -> 400.
    [Fact]
    public async Task CorregirNombres_Retorna400_CuandoElTipoDeLaIdentificacionNoEstaEnLaListaCerrada()
    {
        var validator = new FakeCorregirNombresBodyRequestValidator(BodyValido());
        var router = new FakeCorregirNombresCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "XX-79543210", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con un id invalido");
    }

    // CA-3: numero vacio tras el guion del {id} de ruta -> 400.
    [Fact]
    public async Task CorregirNombres_Retorna400_CuandoElNumeroDeLaIdentificacionQuedaVacio()
    {
        var validator = new FakeCorregirNombresBodyRequestValidator(BodyValido());
        var router = new FakeCorregirNombresCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "CC-", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        router.ComandoRecibido.Should().BeNull("el router nunca deberia invocarse con un id invalido");
    }

    // CA-4: body invalido (PrimerNombre o PrimerApellido vacios) -> 400 Bad Request
    [Fact]
    public async Task CorregirNombres_Retorna400_CuandoElBodyEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeCorregirNombresBodyRequestValidator(error: errorDeValidacion);
        var router = new FakeCorregirNombresCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // CA-2: colaborador inexistente retorna 404 Not Found
    [Fact]
    public async Task CorregirNombres_Retorna404_CuandoColaboradorNoExiste()
    {
        var validator = new FakeCorregirNombresBodyRequestValidator(BodyValido());
        var router = new FakeCorregirNombresCommandRouter(
            lanzar: new KeyNotFoundException("No existe un colaborador registrado con esa identificacion"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), IdValido, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}

// ---- Fakes manuales - NO NSubstitute ----

/// <summary>
/// Fake configurable de IRequestValidator para el body reducido (CorregirNombresBody). Retorna un
/// body pre-configurado o un error segun lo que se le pase en el constructor.
/// </summary>
internal class FakeCorregirNombresBodyRequestValidator : IRequestValidator
{
    private readonly CorregirNombresBody? _body;
    private readonly IActionResult? _error;

    public FakeCorregirNombresBodyRequestValidator(
        CorregirNombresBody? body = null,
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
/// configurada (KeyNotFoundException -> 404). Sin caso InvalidOperationException/409: este comando
/// no tiene reglas de estado (CA-ADR-0030).
/// </summary>
internal class FakeCorregirNombresCommandRouter : ICommandRouter
{
    private readonly Exception? _excepcion;

    public CorregirNombres? ComandoRecibido { get; private set; }

    public FakeCorregirNombresCommandRouter(Exception? lanzar = null) =>
        _excepcion = lanzar;

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (command is CorregirNombres corregirNombres)
            ComandoRecibido = corregirNombres;

        if (_excepcion is not null)
            throw _excepcion;

        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(
        TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
