using System.Text;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarMallaSemanalTurnos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarMallaSemanalTurnos;

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

    private const string Colab = """[{"codigoColaborador":"A","vigenteDesde":"2027-01-01"}]""";

    [Fact]
    public async Task Run_Retorna415_CuandoElContentTypeNoEsJson() =>
        (await EjecutarAsync("text/plain", "{}")).Should().Be(StatusCodes.Status415UnsupportedMediaType);

    [Fact]
    public async Task Run_Retorna400_CuandoElBodyNoEsJsonValido() =>
        (await Json("{ no es json")).Should().Be(StatusCodes.Status400BadRequest);

    [Fact]
    public async Task Run_Retorna400_CuandoElBodyEsNull() =>
        (await Json("null")).Should().Be(StatusCodes.Status400BadRequest);

    [Fact]
    public async Task Run_Retorna422_CuandoFaltaFecha() =>
        (await Json($$"""{"colaboradores":{{Colab}}}""")).Should().Be(StatusCodes.Status422UnprocessableEntity);

    [Fact]
    public async Task Run_Retorna422_CuandoFaltanLosColaboradores() =>
        (await Json("""{"fecha":"2027-03-03"}""")).Should().Be(StatusCodes.Status422UnprocessableEntity);

    [Fact]
    public async Task Run_Retorna422_CuandoLosColaboradoresEstanVacios() =>
        (await Json("""{"fecha":"2027-03-03","colaboradores":[]}""")).Should().Be(StatusCodes.Status422UnprocessableEntity);

    [Fact]
    public async Task Run_Retorna422_CuandoUnColaboradorNoTraeCodigo() =>
        (await Json("""{"fecha":"2027-03-03","colaboradores":[{"vigenteDesde":"2027-01-01"}]}""")).Should().Be(StatusCodes.Status422UnprocessableEntity);

    [Fact]
    public async Task Run_Retorna422_CuandoUnCodigoSeRepite() =>
        (await Json("""{"fecha":"2027-03-03","colaboradores":[{"codigoColaborador":"A","vigenteDesde":"2027-01-01"},{"codigoColaborador":"A","vigenteDesde":"2027-02-01"}]}"""))
            .Should().Be(StatusCodes.Status422UnprocessableEntity);

    [Fact]
    public async Task Run_Retorna422_CuandoVigenteHastaEsAnteriorAVigenteDesde() =>
        (await Json("""{"fecha":"2027-03-03","colaboradores":[{"codigoColaborador":"A","vigenteDesde":"2027-03-08","vigenteHasta":"2027-03-01"}]}"""))
            .Should().Be(StatusCodes.Status422UnprocessableEntity);

    [Fact]
    public async Task Run_Retorna422_CuandoElCursorNoSeDecodifica() =>
        (await Json($$"""{"fecha":"2027-03-03","colaboradores":{{Colab}},"cursor":"@@no-base64@@"}"""))
            .Should().Be(StatusCodes.Status422UnprocessableEntity);
}
