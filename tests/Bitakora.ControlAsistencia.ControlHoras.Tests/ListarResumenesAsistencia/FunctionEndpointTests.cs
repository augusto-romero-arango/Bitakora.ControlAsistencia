// Guards del borde HTTP (415/400/422, CA-6) de la Function QUERY ListarResumenesAsistencia (issue
// #428, MEF-ADR-0042, RFC 10008) -- la unica capa de CA-6 que corre en CI: el smoke test que lo
// cubre end-to-end depende del deploy y el test de composicion (ComposicionServiciosTests) solo
// verifica wiring de IDocumentStore/ITenantResolver.
//
// El IDocumentStore y el ITenantResolver se pasan null! a proposito: los guards deben retornar
// ANTES de abrir la QuerySession, asi que mover la apertura de sesion por encima de ellos rompe
// estos tests con NullReferenceException en vez de pasar inadvertido -- mismo patron que
// ListarAsistenciasDiarias/FunctionEndpointTests.cs (#427) y ListarFichasColaborador/
// FunctionEndpointTests.cs (#373).
//
// Fase roja (projection-test-writer): FunctionEndpoint.Run() hoy SOLO lanza NotImplementedException
// (MEF-ADR-0033, stub minimo de compilacion) -- ninguno de estos tests puede pasar todavia. El
// COMPORTAMIENTO (que status code y que rama dispara cada uno, la agregacion, el keyset y el
// recorte) es responsabilidad de projection-implementer.
//
// No hay guard de "CodigoColaborador obligatorio" (a diferencia de ListarAsistenciasDiarias):
// CodigosColaborador es opcional en este filtro (issue #428, "Universo (ratificado, opcion a)").

using System.Text;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.ControlHoras.ListarResumenesAsistencia;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarResumenesAsistencia;

public class FunctionEndpointTests
{
    private static FunctionEndpoint Endpoint() => new(null!, new TenantResolverFijo());

    private static HttpRequest Request(string? contentType, string body)
    {
        var context = new DefaultHttpContext();
        var bytes = Encoding.UTF8.GetBytes(body);
        context.Request.ContentType = contentType;
        context.Request.ContentLength = bytes.Length;
        context.Request.Body = new MemoryStream(bytes);
        return context.Request;
    }

    private static HttpRequest RequestJson(string body) => Request("application/json", body);

    private static async Task<int?> EjecutarAsync(HttpRequest request)
    {
        var resultado = await Endpoint().Run(request, CancellationToken.None);
        return resultado.Should().BeAssignableTo<ObjectResult>().Subject.StatusCode;
    }

    [Fact]
    public async Task ListarResumenesAsistencia_Retorna415_CuandoElContentTypeNoEsJson()
    {
        var codigo = await EjecutarAsync(Request("text/plain", "{}"));

        codigo.Should().Be(StatusCodes.Status415UnsupportedMediaType);
    }

    [Fact]
    public async Task ListarResumenesAsistencia_Retorna415_CuandoNoViajaContentType()
    {
        var codigo = await EjecutarAsync(Request(contentType: null, body: "{}"));

        codigo.Should().Be(StatusCodes.Status415UnsupportedMediaType);
    }

    [Fact]
    public async Task ListarResumenesAsistencia_Retorna400_CuandoElBodyNoEsJsonValido()
    {
        var codigo = await EjecutarAsync(RequestJson("{ esto no es json valido"));

        codigo.Should().Be(StatusCodes.Status400BadRequest);
    }

    // El literal null es JSON valido y deserializa a null: rama distinta del catch de JsonException.
    [Fact]
    public async Task ListarResumenesAsistencia_Retorna400_CuandoElBodyDeserializaANull()
    {
        var codigo = await EjecutarAsync(RequestJson("null"));

        codigo.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ListarResumenesAsistencia_Retorna422_CuandoFaltanAmbasFechas()
    {
        var codigo = await EjecutarAsync(RequestJson("""{"take":10}"""));

        codigo.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarResumenesAsistencia_Retorna422_CuandoFaltaAlgunaDeLasDosFechas()
    {
        var codigo = await EjecutarAsync(RequestJson("""{"desdeFecha":"2026-07-01"}"""));

        codigo.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarResumenesAsistencia_Retorna422_CuandoElRangoEstaInvertido()
    {
        var codigo = await EjecutarAsync(RequestJson(
            """{"desdeFecha":"2026-07-10","hastaFecha":"2026-07-01"}"""));

        codigo.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }
}
