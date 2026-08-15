// Issue #373 CA-4: validacion del borde HTTP del endpoint QUERY ListarFichasColaborador (MEF-ADR-
// 0042, RFC 10008). Estos casos cortocircuitan ANTES de abrir la QuerySession -- store y
// tenantResolver se pasan nulos a proposito, mismo patron que ListarTurnosVigentes/
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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ListarFichasColaborador;

public class FunctionEndpointTests
{
    private static FunctionEndpoint Endpoint() => new(store: null!, tenantResolver: null!);

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
}
