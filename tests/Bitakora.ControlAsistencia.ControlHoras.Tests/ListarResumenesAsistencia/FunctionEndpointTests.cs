// El IDocumentStore va null! a proposito: los guards (415/400/422) deben retornar antes de abrir la
// QuerySession; si la apertura sube por encima de ellos, estos tests fallan con NullReferenceException.
// Sin guard de CodigoColaborador obligatorio (a diferencia de ListarAsistenciasDiarias): aqui
// CodigosColaborador es opcional.

using System.Text;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarResumenesAsistencia;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarResumenesAsistencia;

public class FunctionEndpointTests
{
    private static FunctionEndpoint Endpoint() => new(null!, null!);

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

    [Fact]
    public async Task ListarResumenesAsistencia_Retorna422_CuandoTakeEsCero()
    {
        var r = await EjecutarAsync(RequestJson("""{"desdeFecha":"2026-07-01","hastaFecha":"2026-07-10","take":0}"""));

        r.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarResumenesAsistencia_Retorna422_CuandoTakeSuperaElMaximo()
    {
        var r = await EjecutarAsync(RequestJson("""{"desdeFecha":"2026-07-01","hastaFecha":"2026-07-10","take":201}"""));

        r.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarResumenesAsistencia_Retorna422_CuandoElCursorNoSeDecodifica()
    {
        var r = await EjecutarAsync(RequestJson("""{"desdeFecha":"2026-07-01","hastaFecha":"2026-07-10","cursor":"@@no-base64@@"}"""));

        r.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarResumenesAsistencia_Retorna422_CuandoElCursorEsUnValorCrudoConLaFormaVieja()
    {
        var r = await EjecutarAsync(RequestJson("""{"desdeFecha":"2026-07-01","hastaFecha":"2026-07-10","cursor":"EMP-001"}"""));

        r.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }
}
