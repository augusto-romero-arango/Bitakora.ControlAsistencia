using System.Text;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.ListarBorradoresDeProgramacion;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ListarBorradoresDeProgramacion;

// CA-5/CA-6: los guards del borde HTTP retornan ANTES de abrir la QuerySession -- el IDocumentStore
// es null! a proposito, cualquier lectura del store lanzaria.
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

    private static async Task<int?> EjecutarAsync(string body, string contentType = "application/json")
    {
        var resultado = await new FunctionEndpoint(null!, null!)
            .Run(Request(contentType, body), CancellationToken.None);
        return resultado.Should().BeAssignableTo<IStatusCodeActionResult>().Subject.StatusCode;
    }

    [Fact]
    public async Task ListarBorradoresDeProgramacion_Retorna415_CuandoElContentTypeNoEsJson() =>
        (await EjecutarAsync("{}", "text/plain")).Should().Be(StatusCodes.Status415UnsupportedMediaType);

    [Fact]
    public async Task ListarBorradoresDeProgramacion_Retorna400_CuandoElBodyNoEsJsonValido() =>
        (await EjecutarAsync("{ no es json")).Should().Be(StatusCodes.Status400BadRequest);

    [Fact]
    public async Task ListarBorradoresDeProgramacion_Retorna400_CuandoElBodyEsNull() =>
        (await EjecutarAsync("null")).Should().Be(StatusCodes.Status400BadRequest);

    [Fact]
    public async Task ListarBorradoresDeProgramacion_Retorna422_CuandoFaltaFechaReferencia() =>
        (await EjecutarAsync("""{"colaboradores":["EMP-001"]}""")).Should().Be(StatusCodes.Status422UnprocessableEntity);

    [Fact]
    public async Task ListarBorradoresDeProgramacion_Retorna422_CuandoDesdeEsPosteriorAHasta() =>
        (await EjecutarAsync("""{"fechaReferencia":"2026-10-13","desde":"2026-10-19","hasta":"2026-10-13"}"""))
            .Should().Be(StatusCodes.Status422UnprocessableEntity);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(201)]
    public async Task ListarBorradoresDeProgramacion_Retorna422_CuandoTakeEstaFueraDeRango(int take) =>
        (await EjecutarAsync($$"""{"fechaReferencia":"2026-10-13","take":{{take}}}"""))
            .Should().Be(StatusCodes.Status422UnprocessableEntity);

    [Fact]
    public async Task ListarBorradoresDeProgramacion_Retorna422_CuandoElCursorEsInvalido() =>
        (await EjecutarAsync("""{"fechaReferencia":"2026-10-13","cursor":"no-es-un-cursor"}"""))
            .Should().Be(StatusCodes.Status422UnprocessableEntity);
}
