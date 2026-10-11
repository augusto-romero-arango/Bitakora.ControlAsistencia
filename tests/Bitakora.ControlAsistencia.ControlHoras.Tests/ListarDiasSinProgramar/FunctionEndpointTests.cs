using System.Text;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarDiasSinProgramar;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarDiasSinProgramar;

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

    private static async Task<int?> EjecutarAsync(string? contentType, string body)
    {
        var resultado = await new FunctionEndpoint(null!, null!).Run(Request(contentType, body), CancellationToken.None);
        return resultado.Should().BeAssignableTo<ObjectResult>().Subject.StatusCode;
    }

    private static Task<int?> Json(string body) => EjecutarAsync("application/json", body);

    private const string Colab = """[{"codigoColaborador":"A","vigenteDesde":"2026-10-01"}]""";

    [Fact]
    public async Task ListarDiasSinProgramar_Retorna415_CuandoElContentTypeNoEsJson() =>
        (await EjecutarAsync("text/plain", "{}")).Should().Be(StatusCodes.Status415UnsupportedMediaType);

    [Fact]
    public async Task ListarDiasSinProgramar_Retorna400_CuandoElBodyNoEsJsonValido() =>
        (await Json("{ no es json")).Should().Be(StatusCodes.Status400BadRequest);

    [Fact]
    public async Task ListarDiasSinProgramar_Retorna422_CuandoFaltaDesdeFecha() =>
        (await Json($$"""{"hastaFecha":"2026-10-11","colaboradores":{{Colab}}}""")).Should().Be(StatusCodes.Status422UnprocessableEntity);

    [Fact]
    public async Task ListarDiasSinProgramar_Retorna422_CuandoFaltaHastaFecha() =>
        (await Json($$"""{"desdeFecha":"2026-10-05","colaboradores":{{Colab}}}""")).Should().Be(StatusCodes.Status422UnprocessableEntity);

    [Fact]
    public async Task ListarDiasSinProgramar_Retorna422_CuandoHastaFechaEsAnteriorADesdeFecha() =>
        (await Json($$"""{"desdeFecha":"2026-10-11","hastaFecha":"2026-10-05","colaboradores":{{Colab}}}""")).Should().Be(StatusCodes.Status422UnprocessableEntity);

    [Fact]
    public async Task ListarDiasSinProgramar_Retorna422_CuandoFaltanLosColaboradores() =>
        (await Json("""{"desdeFecha":"2026-10-05","hastaFecha":"2026-10-11"}""")).Should().Be(StatusCodes.Status422UnprocessableEntity);

    [Fact]
    public async Task ListarDiasSinProgramar_Retorna422_CuandoLosColaboradoresEstanVacios() =>
        (await Json("""{"desdeFecha":"2026-10-05","hastaFecha":"2026-10-11","colaboradores":[]}""")).Should().Be(StatusCodes.Status422UnprocessableEntity);

    [Fact]
    public async Task ListarDiasSinProgramar_Retorna422_CuandoVigenteHastaEsAnteriorAVigenteDesde() =>
        (await Json("""{"desdeFecha":"2026-10-05","hastaFecha":"2026-10-11","colaboradores":[{"codigoColaborador":"A","vigenteDesde":"2026-10-08","vigenteHasta":"2026-10-01"}]}"""))
            .Should().Be(StatusCodes.Status422UnprocessableEntity);

    [Fact]
    public async Task ListarDiasSinProgramar_Retorna422_CuandoElCursorNoSeDecodifica() =>
        (await Json($$"""{"desdeFecha":"2026-10-05","hastaFecha":"2026-10-11","colaboradores":{{Colab}},"cursor":"@@no-base64@@"}"""))
            .Should().Be(StatusCodes.Status422UnprocessableEntity);
}
