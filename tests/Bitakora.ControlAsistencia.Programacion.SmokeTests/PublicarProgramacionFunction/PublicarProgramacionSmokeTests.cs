using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.PublicarProgramacionFunction;

public class PublicarProgramacionSmokeTests(ApiFixture api)
{
    private readonly HttpClient _client = api.Client;

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task PublicarProgramacion_Retorna204SinCuerpo_CuandoElColaboradorNoTieneBorrador()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigo = Guid.CreateVersion7().ToString();

        var response = await _client.PostAsJsonAsync(
            $"/api/programacion/colaboradores/{codigo}/borrador:publicar",
            new { desde = "2027-05-01", hasta = "2027-05-31" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();
    }
}
