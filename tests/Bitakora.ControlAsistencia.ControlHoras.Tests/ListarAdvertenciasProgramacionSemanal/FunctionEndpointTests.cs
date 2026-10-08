using System.Text;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarAdvertenciasProgramacionSemanal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarAdvertenciasProgramacionSemanal;

// Guards del borde HTTP: el store va null! a proposito, los guards retornan antes de abrir sesion.
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
        return resultado.Should().BeAssignableTo<ObjectResult>().Subject.StatusCode;
    }

    [Fact]
    public async Task ListarAdvertenciasProgramacionSemanal_Retorna415_CuandoElContentTypeNoEsJson() =>
        (await EjecutarAsync(Request("text/plain", "{}"))).Should().Be(StatusCodes.Status415UnsupportedMediaType);

    [Fact]
    public async Task ListarAdvertenciasProgramacionSemanal_Retorna400_CuandoElBodyNoEsJsonValido() =>
        (await EjecutarAsync(Request("application/json", "{ no es json"))).Should().Be(StatusCodes.Status400BadRequest);

    [Fact]
    public async Task ListarAdvertenciasProgramacionSemanal_Retorna422_CuandoFaltaLaFecha() =>
        (await EjecutarAsync(Request("application/json", "{}"))).Should().Be(StatusCodes.Status422UnprocessableEntity);

    [Fact]
    public async Task ListarAdvertenciasProgramacionSemanal_Retorna422_CuandoElCursorNoSeDecodifica() =>
        (await EjecutarAsync(Request("application/json", """{"fecha":"2026-10-07","cursor":"@@no-base64@@"}""")))
            .Should().Be(StatusCodes.Status422UnprocessableEntity);
}
