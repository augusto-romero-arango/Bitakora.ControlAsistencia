using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.ObtenerJornada;

public class ObtenerJornadaSmokeTests(ApiFixture api) : IDisposable
{
    private readonly TenantDeTest _tenant = new(api);

    public void Dispose() => _tenant.Dispose();

    private HttpClient _client => _tenant.Client;

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
        var firma = LimitesDePrueba.ObtenerJornada;
        var ct = TestContext.Current.CancellationToken;
        var jornadaId = Guid.CreateVersion7();

        var creada = await _client.PostAsJsonAsync("/api/programacion/jornadas", firma.PayloadCrear(jornadaId), ct);
        creada.StatusCode.Should().Be(HttpStatusCode.Created);
        creada.Headers.Location.Should().NotBeNull();

        var response = await _client.GetAsync(creada.Headers.Location, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("jornadaId").GetGuid().Should().Be(jornadaId);
        body.GetProperty("horasSemanales").GetProperty("horas").GetInt32().Should().Be(firma.SemanalesHoras);
        body.GetProperty("horasSemanales").GetProperty("minutos").GetInt32().Should().Be(firma.SemanalesMinutos);
        body.GetProperty("topeDiario").GetProperty("horas").GetInt32().Should().Be(firma.TopeHoras);
        body.GetProperty("topeDiario").GetProperty("minutos").GetInt32().Should().Be(firma.TopeMinutos);
        body.GetProperty("minimoDiario").GetProperty("horas").GetInt32().Should().Be(firma.MinimoHoras);
        body.GetProperty("minimoDiario").GetProperty("minutos").GetInt32().Should().Be(firma.MinimoMinutos);
        body.GetProperty("diasDescansoPorSemana").GetInt32().Should().Be(firma.DiasDescansoPorSemana);
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
