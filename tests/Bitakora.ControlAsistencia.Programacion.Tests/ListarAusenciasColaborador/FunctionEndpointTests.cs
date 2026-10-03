using System.Text;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.ListarAusenciasColaborador;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ListarAusenciasColaborador;

// Guards del borde HTTP: el IDocumentStore es null! a proposito -- deben retornar ANTES de abrir
// la QuerySession (CA-5: error de validacion sin leer el store).
public class FunctionEndpointTests
{
    private static HttpRequest Request(string? contentType, string body)
    {
        var context = new DefaultHttpContext();
        var bytes = Encoding.UTF8.GetBytes(body);
        context.Request.ContentType = contentType;
        context.Request.ContentLength = bytes.Length;
        context.Request.Body = new MemoryStream(bytes);
        return context.Request;
    }

    private static async Task<int?> EjecutarAsync(HttpRequest request, string codigo = "E001")
    {
        var resultado = await new FunctionEndpoint(null!, null!).Run(request, codigo, CancellationToken.None);
        return resultado.Should().BeAssignableTo<IStatusCodeActionResult>().Subject.StatusCode;
    }

    [Fact]
    public async Task ListarAusenciasColaborador_Retorna415_CuandoElContentTypeNoEsJson()
    {
        (await EjecutarAsync(Request("text/plain", "{}"))).Should().Be(StatusCodes.Status415UnsupportedMediaType);
    }

    [Fact]
    public async Task ListarAusenciasColaborador_Retorna400_CuandoElCodigoNoEsUrlSafe()
    {
        var codigo = await EjecutarAsync(
            Request("application/json", """{"desde":"2026-10-01","hasta":"2026-10-31"}"""), "E 001/x");

        codigo.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ListarAusenciasColaborador_Retorna400_CuandoElBodyNoEsJsonValido()
    {
        (await EjecutarAsync(Request("application/json", "{ no es json"))).Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ListarAusenciasColaborador_Retorna422_CuandoFaltaDesde()
    {
        var codigo = await EjecutarAsync(Request("application/json", """{"hasta":"2026-10-31"}"""));

        codigo.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarAusenciasColaborador_Retorna422_CuandoFaltaHasta()
    {
        var codigo = await EjecutarAsync(Request("application/json", """{"desde":"2026-10-01"}"""));

        codigo.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarAusenciasColaborador_Retorna422_CuandoDesdeEsPosteriorAHasta()
    {
        var codigo = await EjecutarAsync(
            Request("application/json", """{"desde":"2026-10-31","hasta":"2026-10-01"}"""));

        codigo.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }
}
