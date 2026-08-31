// Guards del borde HTTP (415/400/422) de la Function QUERY, la unica capa de CA-3 que corre en CI:
// el smoke test que lo cubre end-to-end depende del deploy y el test de composicion solo verifica
// wiring.
//
// A diferencia del precedente ListarAsistenciasDiarias, CodigoColaborador y SedeId son opcionales:
// su ausencia no produce ningun 422 propio.
//
// El IDocumentStore se pasa null! a proposito: los tres guards deben retornar ANTES de abrir la
// QuerySession, asi que mover la apertura de sesion por encima de ellos rompe estos tests con
// NullReferenceException en vez de pasar inadvertido.

using System.Text;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.ControlHoras.ListarTurnosVigentes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarTurnosVigentes;

public class FunctionEndpointTests
{
    private static FunctionEndpoint Endpoint() => new(null!, new TenantResolverMonoTenantPorDefecto());

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

    private static async Task<ObjectResult> EjecutarAsync(HttpRequest request)
    {
        var resultado = await Endpoint().Run(request, CancellationToken.None);
        return resultado.Should().BeAssignableTo<ObjectResult>().Subject;
    }

    [Fact]
    public async Task ListarTurnosVigentes_Retorna415_CuandoElContentTypeNoEsJson()
    {
        var resultado = await EjecutarAsync(Request("text/plain", "{}"));

        resultado.StatusCode.Should().Be(StatusCodes.Status415UnsupportedMediaType);
    }

    [Fact]
    public async Task ListarTurnosVigentes_Retorna415_CuandoNoViajaContentType()
    {
        var resultado = await EjecutarAsync(Request(contentType: null, body: "{}"));

        resultado.StatusCode.Should().Be(StatusCodes.Status415UnsupportedMediaType);
    }

    // El mensaje se afirma ademas del StatusCode: un 400 puede venir de este catch o del guard de
    // body nulo, y solo el texto distingue cual de los dos respondio.
    [Fact]
    public async Task ListarTurnosVigentes_Retorna400_CuandoElBodyNoEsJsonValido()
    {
        var resultado = await EjecutarAsync(RequestJson("{ esto no es json valido"));

        resultado.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        resultado.Value.Should().BeOfType<string>().Which.Should().Contain("JSON");
    }

    // El literal null es JSON valido y deserializa a null: rama distinta del catch de
    // JsonException.
    [Fact]
    public async Task ListarTurnosVigentes_Retorna400_CuandoElBodyDeserializaANull()
    {
        var resultado = await EjecutarAsync(RequestJson("null"));

        resultado.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        resultado.Value.Should().BeOfType<string>().Which.Should().Contain("query");
    }

    [Fact]
    public async Task ListarTurnosVigentes_Retorna422_CuandoDesdeFechaEstaAusente()
    {
        var resultado = await EjecutarAsync(RequestJson("""{"hastaFecha":"2026-05-10"}"""));

        resultado.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarTurnosVigentes_Retorna422_CuandoHastaFechaEstaAusente()
    {
        var resultado = await EjecutarAsync(RequestJson("""{"desdeFecha":"2026-05-01"}"""));

        resultado.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarTurnosVigentes_Retorna422_CuandoElFiltroLlegaVacio()
    {
        var resultado = await EjecutarAsync(RequestJson("{}"));

        resultado.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarTurnosVigentes_Retorna422_CuandoElRangoEstaInvertido()
    {
        var resultado = await EjecutarAsync(RequestJson(
            """{"desdeFecha":"2026-05-10","hastaFecha":"2026-05-05"}"""));

        resultado.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarTurnosVigentes_Retorna422_CuandoCodigoColaboradorYSedeIdSonValidosPeroElRangoNoLoEs()
    {
        var resultado = await EjecutarAsync(RequestJson(
            """
            {"desdeFecha":"2026-05-10","hastaFecha":"2026-05-05","codigoColaborador":"EMP-001","sedeId":"SD-SUBA"}
            """));

        resultado.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }
}
