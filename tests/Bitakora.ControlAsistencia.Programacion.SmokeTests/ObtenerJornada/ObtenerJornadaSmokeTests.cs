using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.ObtenerJornada;

public class ObtenerJornadaSmokeTests(ApiFixture api)
{
    private readonly HttpClient _client = api.Client;

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task HealthCheck_DebeResponder200_CuandoElEntornoEstaDisponible()
    {
        var response = await _client.GetAsync("/api/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerJornada_DebeRetornar200ConLaJornada_CuandoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var minutos = Random.Shared.Next(1, 60);
        var payload = new
        {
            jornadaId = Guid.CreateVersion7(),
            horasSemanales = new { horas = 30, minutos },
            topeDiario = new { horas = 8, minutos = 0 },
            minimoDiario = new { horas = 0, minutos = 0 },
            diasDescansoPorSemana = 1
        };

        var creada = await _client.PostAsJsonAsync("/api/programacion/jornadas", payload, ct);
        creada.StatusCode.Should().Be(HttpStatusCode.Created);
        creada.Headers.Location.Should().NotBeNull();

        var response = await _client.GetAsync(creada.Headers.Location, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("jornadaId").GetGuid().Should().Be(payload.jornadaId);
        body.GetProperty("horasSemanales").GetProperty("horas").GetInt32().Should().Be(30);
        body.GetProperty("horasSemanales").GetProperty("minutos").GetInt32().Should().Be(minutos);
        body.GetProperty("topeDiario").GetProperty("horas").GetInt32().Should().Be(8);
        body.GetProperty("topeDiario").GetProperty("minutos").GetInt32().Should().Be(0);
        body.GetProperty("minimoDiario").GetProperty("horas").GetInt32().Should().Be(0);
        body.GetProperty("minimoDiario").GetProperty("minutos").GetInt32().Should().Be(0);
        body.GetProperty("diasDescansoPorSemana").GetInt32().Should().Be(1);
        body.GetProperty("descripcion").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerJornada_DebeRetornar404_CuandoLaJornadaNoExiste()
    {
        var response = await _client.GetAsync(
            $"/api/programacion/jornadas/{Guid.CreateVersion7()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerJornada_DebeRetornar400_CuandoElIdNoEsGuid()
    {
        var response = await _client.GetAsync(
            "/api/programacion/jornadas/no-es-guid", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
