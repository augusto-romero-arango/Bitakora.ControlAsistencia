using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.ListarBorradoresDeProgramacion;

public class ListarBorradoresDeProgramacionSmokeTests(ApiFixture api)
{
    private readonly HttpClient _client = api.Client;

    private static readonly HttpMethod MetodoQuery = new("QUERY");

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record ListaSmoke(JsonElement Elementos, string? SiguienteCursor);

    private async Task<HttpResponseMessage> ConsultarAsync(object? filtro, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(MetodoQuery, "/api/programacion/borradores")
        {
            Content = JsonContent.Create(filtro)
        };
        return await _client.SendAsync(request, ct);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task Health_RetornaOk()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarBorradoresDeProgramacion_Retorna200ConElSobre_CuandoSeEnviaFechaReferencia()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(
            new { fechaReferencia = "2027-05-01", colaboradores = new[] { Guid.CreateVersion7().ToString() } }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var lista = await response.Content.ReadFromJsonAsync<ListaSmoke>(JsonOptions, ct);
        lista!.Elementos.ValueKind.Should().Be(JsonValueKind.Array);
        lista.Elementos.GetArrayLength().Should().Be(0);
        lista.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarBorradoresDeProgramacion_Retorna422_CuandoElCursorEsInvalido()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(
            new { fechaReferencia = "2027-05-01", cursor = "no-es-un-cursor" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarBorradoresDeProgramacion_Retorna422_CuandoFaltaFechaReferencia()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(new { desde = "2027-05-01", hasta = "2027-05-31" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarBorradoresDeProgramacion_Retorna422_CuandoDesdeEsPosteriorAHasta()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(
            new { fechaReferencia = "2027-05-01", desde = "2027-05-31", hasta = "2027-05-01" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarBorradoresDeProgramacion_Retorna400_CuandoElBodyNoEsJsonValido()
    {
        var ct = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(MetodoQuery, "/api/programacion/borradores")
        {
            Content = new StringContent("{ esto no es json valido", Encoding.UTF8, "application/json")
        };

        var response = await _client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
