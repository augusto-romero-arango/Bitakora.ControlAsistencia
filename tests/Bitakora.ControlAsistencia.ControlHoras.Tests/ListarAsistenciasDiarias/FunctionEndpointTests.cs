// Guards del borde HTTP (415/400/422) de la Function QUERY, la unica capa de CA-4 que corre en CI:
// el smoke test que lo cubre end-to-end depende del deploy y el test de composicion solo verifica
// wiring.
//
// El IDocumentStore se pasa null! a proposito: los tres guards deben retornar ANTES de abrir la
// QuerySession, asi que mover la apertura de sesion por encima de ellos rompe estos tests con
// NullReferenceException en vez de pasar inadvertido.

using System.Text;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.ControlHoras.ListarAsistenciasDiarias;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarAsistenciasDiarias;

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
    public async Task ListarAsistenciasDiarias_Retorna415_CuandoElContentTypeNoEsJson()
    {
        var codigo = await EjecutarAsync(Request("text/plain", "{}"));

        codigo.Should().Be(StatusCodes.Status415UnsupportedMediaType);
    }

    [Fact]
    public async Task ListarAsistenciasDiarias_Retorna415_CuandoNoViajaContentType()
    {
        var codigo = await EjecutarAsync(Request(contentType: null, body: "{}"));

        codigo.Should().Be(StatusCodes.Status415UnsupportedMediaType);
    }

    [Fact]
    public async Task ListarAsistenciasDiarias_Retorna400_CuandoElBodyNoEsJsonValido()
    {
        var codigo = await EjecutarAsync(RequestJson("{ esto no es json valido"));

        codigo.Should().Be(StatusCodes.Status400BadRequest);
    }

    // El literal null es JSON valido y deserializa a null: rama distinta del catch de JsonException.
    [Fact]
    public async Task ListarAsistenciasDiarias_Retorna400_CuandoElBodyDeserializaANull()
    {
        var codigo = await EjecutarAsync(RequestJson("null"));

        codigo.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ListarAsistenciasDiarias_Retorna422_CuandoElCodigoDeColaboradorEstaAusente()
    {
        var codigo = await EjecutarAsync(RequestJson(
            """{"desdeFecha":"2026-07-01","hastaFecha":"2026-07-05"}"""));

        codigo.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarAsistenciasDiarias_Retorna422_CuandoElCodigoDeColaboradorEsSoloEspacios()
    {
        var codigo = await EjecutarAsync(RequestJson(
            """{"codigoColaborador":"   ","desdeFecha":"2026-07-01","hastaFecha":"2026-07-05"}"""));

        codigo.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarAsistenciasDiarias_Retorna422_CuandoFaltaAlgunaDeLasDosFechas()
    {
        var codigo = await EjecutarAsync(RequestJson(
            """{"codigoColaborador":"E001","desdeFecha":"2026-07-01"}"""));

        codigo.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarAsistenciasDiarias_Retorna422_CuandoElRangoEstaInvertido()
    {
        var codigo = await EjecutarAsync(RequestJson(
            """{"codigoColaborador":"E001","desdeFecha":"2026-07-10","hastaFecha":"2026-07-01"}"""));

        codigo.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }
}
