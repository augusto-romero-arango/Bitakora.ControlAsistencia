// Store y tenantContext nulos a proposito: estos casos cortocircuitan antes de abrir la QuerySession,
// y si la validacion se moviera despues de tocar Marten se pondrian rojos.
// ReadFromJsonAsync (STJ por reflection) no lanza por un campo string/DateOnly ausente: lo deja en
// su default (null / 0001-01-01); JsonException solo sale con body vacio o JSON invalido. Por eso el
// 422 por FechaReferencia ausente o cursor incompleto exige validacion explicita.

using System.Text;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.ListarFichasColaborador;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ListarFichasColaborador;

public class FunctionEndpointTests
{
    private static FunctionEndpoint Endpoint() => new(store: null!, tenantContext: null!);

    private static HttpRequest FakeHttpRequest(string? contentType, string? body)
    {
        var context = new DefaultHttpContext();

        if (contentType is not null)
            context.Request.ContentType = contentType;

        if (body is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentLength = bytes.Length;
        }

        return context.Request;
    }

    [Fact]
    public async Task ListarFichasColaborador_Retorna415_CuandoElContentTypeNoEsJson()
    {
        var request = FakeHttpRequest(contentType: "text/plain", body: "{}");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        var objectResult = resultado.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status415UnsupportedMediaType);
        objectResult.Value.Should().BeOfType<string>();
    }

    [Fact]
    public async Task ListarFichasColaborador_Retorna415_CuandoElContentTypeEstaAusente()
    {
        var request = FakeHttpRequest(contentType: null, body: "{}");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        var objectResult = resultado.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status415UnsupportedMediaType);
    }

    [Fact]
    public async Task ListarFichasColaborador_Retorna400_CuandoElBodyEstaAusente()
    {
        var request = FakeHttpRequest(contentType: "application/json", body: null);

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        var badRequest = resultado.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeOfType<string>();
    }

    [Fact]
    public async Task ListarFichasColaborador_Retorna400_CuandoElBodyNoEsJsonValido()
    {
        var request = FakeHttpRequest(contentType: "application/json", body: "{ esto no es json");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        resultado.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ListarFichasColaborador_Retorna400_CuandoElBodyEsElLiteralJsonNull()
    {
        var request = FakeHttpRequest(contentType: "application/json", body: "null");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        resultado.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ListarFichasColaborador_Retorna422_CuandoFaltaFechaReferencia()
    {
        // FechaReferencia es obligatoria: el back jamas resuelve "hoy".
        var request = FakeHttpRequest(contentType: "application/json", body: """{"take":10}""");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        var unprocessable = resultado.Should().BeOfType<ObjectResult>().Subject;
        unprocessable.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        unprocessable.Value.Should().BeOfType<string>();
    }

    [Fact]
    public async Task ListarFichasColaborador_Retorna422_CuandoUnaEtiquetaDelFiltroEsInvalida()
    {
        // Etiqueta.Crear rechaza la categoria vacia con ArgumentException, que el endpoint traduce a 422.
        var request = FakeHttpRequest(
            contentType: "application/json",
            body: """{"fechaReferencia":"2026-08-14","etiquetas":[{"categoria":"","valor":"Tecnologia"}]}""");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        var unprocessable = resultado.Should().BeOfType<ObjectResult>().Subject;
        unprocessable.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    // STJ acepta un elemento null dentro del array pese a la anotacion no-nullable de FiltroEtiqueta:
    // sin el guard, la NullReferenceException escapaba del catch de ArgumentException como 500.
    [Fact]
    public async Task ListarFichasColaborador_Retorna422_CuandoUnaEtiquetaDelFiltroEsNula()
    {
        var request = FakeHttpRequest(
            contentType: "application/json",
            body: """{"fechaReferencia":"2026-08-14","etiquetas":[null]}""");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        var unprocessable = resultado.Should().BeOfType<ObjectResult>().Subject;
        unprocessable.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        unprocessable.Value.Should().BeOfType<string>();
    }

    // Sede ausente no filtra, pero presente en blanco es error del cliente: ninguna ficha lleva sede en
    // blanco (AsignarSedeBodyValidator exige NotEmpty) y un 200 vacio lo esconderia.
    [Fact]
    public async Task ListarFichasColaborador_Retorna422_CuandoElCodigoSedeDelFiltroEstaEnBlanco()
    {
        var request = FakeHttpRequest(
            contentType: "application/json",
            body: """{"fechaReferencia":"2026-08-14","codigoSede":"   "}""");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        var unprocessable = resultado.Should().BeOfType<ObjectResult>().Subject;
        unprocessable.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        unprocessable.Value.Should().BeOfType<string>();
    }

    // Ninguna ficha puede tener Guid.Empty (AsignarJornadaBodyValidator exige NotEmpty).
    [Fact]
    public async Task ListarFichasColaborador_Retorna422_CuandoElJornadaIdDelFiltroEsGuidVacio()
    {
        var request = FakeHttpRequest(
            contentType: "application/json",
            body: """{"fechaReferencia":"2026-08-14","jornadaId":"00000000-0000-0000-0000-000000000000"}""");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        var unprocessable = resultado.Should().BeOfType<ObjectResult>().Subject;
        unprocessable.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        unprocessable.Value.Should().BeOfType<string>();
    }

    [Fact]
    public async Task ListarFichasColaborador_Retorna400_CuandoElJornadaIdDelFiltroNoEsGuid()
    {
        var request = FakeHttpRequest(
            contentType: "application/json",
            body: """{"fechaReferencia":"2026-08-14","jornadaId":"no-es-un-guid"}""");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        resultado.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ListarFichasColaborador_NoRechazaElFiltro_CuandoElJornadaIdEsValido()
    {
        var request = FakeHttpRequest(
            contentType: "application/json",
            body: """{"fechaReferencia":"2026-08-14","jornadaId":"3f2c8a52-5b0e-4a6e-9f1d-7a1c2b3d4e5f"}""");

        var act = async () => await Endpoint().Run(request, CancellationToken.None);

        await act.Should().ThrowExactlyAsync<NullReferenceException>();
    }

    [Fact]
    public async Task ListarFichasColaborador_NoRechazaElFiltro_CuandoElJornadaIdEsNulo()
    {
        var request = FakeHttpRequest(
            contentType: "application/json",
            body: """{"fechaReferencia":"2026-08-14","jornadaId":null}""");

        var act = async () => await Endpoint().Run(request, CancellationToken.None);

        await act.Should().ThrowExactlyAsync<NullReferenceException>();
    }

    [Fact]
    public async Task ListarFichasColaborador_Retorna422_CuandoElCursorTraeUnSoloCampo()
    {
        // Cursor con NombreCompleto pero sin Id (el otro campo del cursor keyset).
        var request = FakeHttpRequest(
            contentType: "application/json",
            body: """{"fechaReferencia":"2026-08-14","cursor":{"nombreCompleto":"Ana Torres"}}""");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        var unprocessable = resultado.Should().BeOfType<ObjectResult>().Subject;
        unprocessable.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarFichasColaborador_NoRechazaElFiltro_CuandoNoTraeTake()
    {
        var request = FakeHttpRequest(contentType: "application/json", body: """{"fechaReferencia":"2026-08-14"}""");

        var act = async () => await Endpoint().Run(request, CancellationToken.None);

        // store nulo: superar la validacion del borde equivale a llegar a la QuerySession.
        await act.Should().ThrowExactlyAsync<NullReferenceException>();
    }

    [Fact]
    public async Task ListarFichasColaborador_Retorna422_CuandoTakeEsCero()
    {
        var request = FakeHttpRequest("application/json", """{"fechaReferencia":"2026-08-14","take":0}""");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        resultado.Should().BeOfType<ObjectResult>().Which.StatusCode
            .Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarFichasColaborador_Retorna422_CuandoTakeSuperaElMaximoDe200()
    {
        var request = FakeHttpRequest("application/json", """{"fechaReferencia":"2026-08-14","take":201}""");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        resultado.Should().BeOfType<ObjectResult>().Which.StatusCode
            .Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarFichasColaborador_Retorna422_CuandoElCursorEsIndescifrable()
    {
        var request = FakeHttpRequest("application/json", """{"fechaReferencia":"2026-08-14","cursor":"esto-no-es-un-cursor"}""");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        resultado.Should().BeOfType<ObjectResult>().Which.StatusCode
            .Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarFichasColaborador_Retorna422_CuandoElCursorTraeLaFormaVieja()
    {
        var request = FakeHttpRequest(
            "application/json",
            """{"fechaReferencia":"2026-08-14","cursor":{"nombreCompleto":"Ana Torres","id":"CC-1"}}""");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        resultado.Should().BeOfType<ObjectResult>().Which.StatusCode
            .Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarFichasColaborador_NoRechazaElTake_CuandoEsElMaximoDe200()
    {
        var request = FakeHttpRequest("application/json", """{"fechaReferencia":"2026-08-14","take":200}""");

        var act = async () => await Endpoint().Run(request, CancellationToken.None);

        await act.Should().ThrowExactlyAsync<NullReferenceException>();
    }

    [Fact]
    public void CursorFicha_RecuperaLasClavesKeyset_CuandoSeDecodificaLoCodificado()
    {
        var cursor = CursorFicha.Codificar("Ana María Torres", "CC-1098765432");

        CursorFicha.TryDecodificar(cursor, out var decodificado).Should().BeTrue();
        decodificado.Should().Be(new CursorFicha("Ana María Torres", "CC-1098765432"));
        cursor.Should().MatchRegex("^[A-Za-z0-9_-]+$");
    }

    [Fact]
    public void CursorFicha_NoDecodifica_CuandoElTextoEsIndescifrable()
    {
        CursorFicha.TryDecodificar("esto no es base64url!", out var decodificado).Should().BeFalse();
        decodificado.Should().BeNull();
    }

    private static FichaColaborador Ficha(string id, string nombre) => new(
        Id: id, NombreCompleto: nombre, CodigoColaborador: "COL-" + id,
        VigenteDesde: new DateOnly(2025, 1, 1), VigenteHasta: FichaColaborador.CentinelaVigenciaAbierta,
        Etiquetas: [], EtiquetasNormalizadas: new Dictionary<string, string>());

    [Fact]
    public void Componer_DevuelveTodoSinCursor_CuandoNoHayTake()
    {
        var lista = FunctionEndpoint.Componer([Ficha("CC-1", "Ana"), Ficha("CC-2", "Beto")], take: null);

        lista.Elementos.Select(e => e.Id).Should().Equal("CC-1", "CC-2");
        lista.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Componer_NoTraeCursor_CuandoLaPaginaNoTieneMasFilas()
    {
        var lista = FunctionEndpoint.Componer([Ficha("CC-1", "Ana"), Ficha("CC-2", "Beto")], take: 2);

        lista.Elementos.Should().HaveCount(2);
        lista.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Componer_RecortaALaPaginaYApuntaALaUltimaFilaEntregada_CuandoHayUnaFilaDeMas()
    {
        var lista = FunctionEndpoint.Componer(
            [Ficha("CC-1", "Ana"), Ficha("CC-2", "Beto"), Ficha("CC-3", "Carla")], take: 2);

        lista.Elementos.Should().HaveCount(2);
        lista.SiguienteCursor.Should().NotBeNull();
        CursorFicha.TryDecodificar(lista.SiguienteCursor!, out var cursor).Should().BeTrue();
        cursor.Should().Be(new CursorFicha("Beto", "CC-2"));
    }

    [Fact]
    public void Componer_DevuelveListaVacia_CuandoNoHayFichas()
    {
        var lista = FunctionEndpoint.Componer([], take: 10);

        lista.Elementos.Should().BeEmpty();
        lista.SiguienteCursor.Should().BeNull();
    }
}
