using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.PublicarProgramacionFunction;

public class PublicarProgramacionSmokeTests(ApiFixture api)
{
    private readonly HttpClient _client = api.Client;

    private static string Ruta(string codigo) =>
        $"/api/programacion/colaboradores/{codigo}/borrador:publicar";

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
    public async Task PublicarProgramacion_Retorna204_CuandoColaboradorNoTieneBorrador()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigo = Guid.CreateVersion7().ToString();

        var response = await _client.PostAsJsonAsync(
            Ruta(codigo), new { desde = "2027-07-28", hasta = "2027-08-03" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task PublicarProgramacion_Retorna400_CuandoFaltaHasta()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigo = Guid.CreateVersion7().ToString();

        var response = await _client.PostAsJsonAsync(Ruta(codigo), new { desde = "2027-07-28" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task PublicarProgramacion_Retorna400_CuandoFechaMalFormada()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigo = Guid.CreateVersion7().ToString();

        var response = await _client.PostAsJsonAsync(
            Ruta(codigo), new { desde = "no-es-fecha", hasta = "2027-08-03" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
