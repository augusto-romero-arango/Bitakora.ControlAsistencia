using System.Text;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.ListarAusenciasDelEquipo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ListarAusenciasDelEquipo;

// CA-5: los guards del borde HTTP retornan ANTES de abrir la QuerySession -- el IDocumentStore es
// null! a proposito, cualquier lectura del store lanzaria.
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

    private static async Task<int?> EjecutarAsync(HttpRequest request)
    {
        var resultado = await new FunctionEndpoint(null!, null!).Run(request, CancellationToken.None);
        return resultado.Should().BeAssignableTo<IStatusCodeActionResult>().Subject.StatusCode;
    }

    [Fact]
    public async Task ListarAusenciasDelEquipo_Retorna415_CuandoElContentTypeNoEsJson()
    {
        (await EjecutarAsync(Request("text/plain", "{}"))).Should().Be(StatusCodes.Status415UnsupportedMediaType);
    }

    [Fact]
    public async Task ListarAusenciasDelEquipo_Retorna400_CuandoElBodyNoEsJsonValido()
    {
        (await EjecutarAsync(Request("application/json", "{ no es json"))).Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ListarAusenciasDelEquipo_Retorna422_CuandoFaltaDesde()
    {
        var codigo = await EjecutarAsync(Request("application/json", """{"hasta":"2026-10-19"}"""));

        codigo.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarAusenciasDelEquipo_Retorna422_CuandoFaltaHasta()
    {
        var codigo = await EjecutarAsync(Request("application/json", """{"desde":"2026-10-13"}"""));

        codigo.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarAusenciasDelEquipo_Retorna422_CuandoDesdeEsPosteriorAHasta()
    {
        var codigo = await EjecutarAsync(
            Request("application/json", """{"desde":"2026-10-19","hasta":"2026-10-13"}"""));

        codigo.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }
}
