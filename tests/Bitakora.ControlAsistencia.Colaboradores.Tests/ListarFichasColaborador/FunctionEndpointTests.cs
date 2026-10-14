// Issue #373 CA-4: validacion del borde HTTP del endpoint QUERY ListarFichasColaborador (MEF-ADR-
// 0042, RFC 10008). Estos casos cortocircuitan ANTES de abrir la QuerySession -- store y
// tenantContext se pasan nulos a proposito, mismo patron que ListarTurnosVigentes/
// FunctionEndpointTests.cs (ControlHoras.Tests, issue #329): si un cambio futuro moviera la
// validacion DESPUES de tocar Marten, estos tests se pondrian rojos por la razon correcta.
//
// Fase roja (projection-test-writer): FunctionEndpoint.Run() hoy SOLO lanza NotImplementedException
// (MEF-ADR-0033, stub minimo de compilacion) -- ninguno de estos tests puede pasar todavia. El
// COMPORTAMIENTO (que status code y que rama dispara cada uno) es responsabilidad de
// projection-implementer; el MECANISMO exacto (required del record, chequeo explicito de default,
// etc., segun el propio issue) es su decision, no la de este archivo.
//
// Los cinco casos de deserializacion (body ausente, JSON invalido, literal "null", FechaReferencia
// ausente, cursor incompleto) estan verificados por spike propio contra
// HttpRequestJsonExtensions.ReadFromJsonAsync (STJ reflection-based, sin JsonOptions en
// RequestServices): un campo string/DateOnly ausente en el JSON NO lanza -- se completa con el
// default del tipo (null / 0001-01-01) sin excepcion -- y JsonException solo aparece con body vacio
// o JSON sintacticamente invalido. De ahi que CA-4 exija una validacion EXPLICITA post-deserializacion
// para el 422 (missing FechaReferencia, cursor con un solo campo): STJ por si solo no la produce.

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

    // --- 415: Content-Type ausente o distinto de application/json (RFC 10008 seccion 2) ---

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

    // --- 400: body ausente, JSON invalido o literal "null" (RFC 10008 seccion 2.1) ---

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
        // JSON sintacticamente valido que deserializa a null (distinto del caso anterior, JSON
        // invalido) -- misma rama que el ejemplo canonico de skills/projections/read-apis.md:
        // "if (filtro is null) return new BadRequestObjectResult(...)".
        var request = FakeHttpRequest(contentType: "application/json", body: "null");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        resultado.Should().BeOfType<BadRequestObjectResult>();
    }

    // --- 422: JSON valido pero no procesable (CA-4) ---

    [Fact]
    public async Task ListarFichasColaborador_Retorna422_CuandoFaltaFechaReferencia()
    {
        // FechaReferencia es obligatoria (el back jamas resuelve "hoy") -- STJ no lanza por su
        // ausencia (verificado por spike: el campo queda en 0001-01-01), asi que el 422 depende de
        // una validacion explicita del implementer, no de la deserializacion.
        var request = FakeHttpRequest(contentType: "application/json", body: """{"take":10}""");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        var unprocessable = resultado.Should().BeOfType<ObjectResult>().Subject;
        unprocessable.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        unprocessable.Value.Should().BeOfType<string>();
    }

    [Fact]
    public async Task ListarFichasColaborador_Retorna422_CuandoUnaEtiquetaDelFiltroEsInvalida()
    {
        // CA-2: normalizacion simetrica -- el endpoint construye Etiqueta.Crear(Categoria, Valor)
        // con cada par; un par con categoria vacia lo rechaza Etiqueta.Crear con ArgumentException
        // (Colaboradores.DomainEvents/Etiqueta.cs), que el endpoint traduce a 422.
        var request = FakeHttpRequest(
            contentType: "application/json",
            body: """{"fechaReferencia":"2026-08-14","etiquetas":[{"categoria":"","valor":"Tecnologia"}]}""");

        var resultado = await Endpoint().Run(request, CancellationToken.None);

        var unprocessable = resultado.Should().BeOfType<ObjectResult>().Subject;
        unprocessable.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    // Agregado en la revision: el body es entrada del cliente y STJ acepta un elemento null dentro
    // del array pese a la anotacion no-nullable de FiltroEtiqueta. Sin el guard del endpoint, el
    // acceso a par.Categoria era una NullReferenceException que escapaba del catch de
    // ArgumentException y salia como 500 donde RFC 10008 seccion 2.1 pide 422.
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

    // Agregado en la revision (issue #519 CA-4): el filtro por sede es opcional -- ausente/null no
    // filtra -- pero presente y en blanco no es una consulta legitima: ninguna ficha puede llevar
    // sede en blanco (AsignarSedeBodyValidator exige NotEmpty del lado comando), asi que responder
    // 200 con lista vacia esconderia un error del cliente. Misma rama 422 que una etiqueta invalida.
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
        // Cursor con NombreCompleto pero sin Id (el otro campo del cursor keyset, CursorFicha.Id
        // queda null -- verificado por spike: STJ no lanza por un campo string ausente).
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

    // --- CA-4: entrada invalida del sobre unico (CA-ADR-0039) -> 422 ---

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

    // --- Cursor opaco ---

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

    // --- Sobre { elementos, siguienteCursor } (CA-2) ---

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
