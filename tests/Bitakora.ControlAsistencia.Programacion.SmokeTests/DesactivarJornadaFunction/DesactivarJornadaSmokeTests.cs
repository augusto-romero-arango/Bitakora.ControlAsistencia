using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.DesactivarJornadaFunction;

public class DesactivarJornadaSmokeTests(ApiFixture api, PostgresFixture postgres) : IDisposable
{
    private readonly TenantDeTest _tenant = new(api);

    public void Dispose() => _tenant.Dispose();

    private const string RutaJornadas = "/api/programacion/jornadas";
    private const string TipoEvento = "jornada_desactivada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private HttpClient _client => _tenant.Client;

    private async Task<Guid> CrearJornadaAsync(FirmaDeLimites firma, CancellationToken ct)
    {
        var jornadaId = Guid.CreateVersion7();
        (await _client.PostAsJsonAsync(RutaJornadas, firma.PayloadCrear(jornadaId), ct))
            .StatusCode.Should().Be(HttpStatusCode.Created, "el arrange depende de que CrearJornada funcione");
        return jornadaId;
    }

    private static string RutaDesactivar(Guid id) => $"{RutaJornadas}/{id}:desactivar";

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task HealthCheck_DebeResponder200()
    {
        var response = await _client.GetAsync("/api/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DesactivarJornada_DebeRetornar204YMarcarInactiva_CuandoNoEstaEnUso()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");
        var ct = TestContext.Current.CancellationToken;
        var id = await CrearJornadaAsync(LimitesDePrueba.DesactivarJornadaPersiste, ct);

        var desactivada = await _client.PostAsync(RutaDesactivar(id), null, ct);

        desactivada.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var obtenida = await _client.GetAsync($"{RutaJornadas}/{id}", ct);
        obtenida.StatusCode.Should().Be(HttpStatusCode.OK);
        (await obtenida.Content.ReadFromJsonAsync<JsonElement>(ct))
            .GetProperty("activa").GetBoolean().Should().BeFalse();

        var repetida = await _client.PostAsync(RutaDesactivar(id), null, ct);

        repetida.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await postgres.ContarEventosAsync(PostgresFixture.SchemaProgramacion, id.ToString(), TipoEvento))
            .Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DesactivarJornada_DebeRetornar409_CuandoEsLaPredeterminada()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await CrearJornadaAsync(LimitesDePrueba.DesactivarJornadaPredeterminada, ct);
        (await _client.PutAsJsonAsync("/api/programacion/preferencias/jornada-predeterminada", new { jornadaId = id }, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await _client.PostAsync(RutaDesactivar(id), null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DesactivarJornada_DebeRetornar409_CuandoLaUsaUnaPlantilla()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await CrearJornadaAsync(LimitesDePrueba.DesactivarJornadaEnPlantilla, ct);
        var plantillaId = Guid.CreateVersion7();
        (await _client.PostAsJsonAsync("/api/programacion/plantillas-semanales", new
        {
            plantillaId,
            nombre = $"[TEST] Plantilla {plantillaId}",
            semanas = 1
        }, ct)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await _client.PutAsJsonAsync($"/api/programacion/plantillas-semanales/{plantillaId}/jornada",
            new { jornadaId = id }, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var reflejada = await Polling.WaitUntilTrueAsync(async () =>
        {
            var cuadro = await _client.GetAsync($"/api/programacion/plantillas-semanales/{plantillaId}", ct);
            return cuadro.IsSuccessStatusCode
                && (await cuadro.Content.ReadAsStringAsync(ct)).Contains(id.ToString(), StringComparison.OrdinalIgnoreCase);
        }, Timeout);
        reflejada.Should().BeTrue("CuadroSemanalTurnos es una proyeccion Async");

        var response = await _client.PostAsync(RutaDesactivar(id), null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync(ct)).Should().Contain(plantillaId.ToString());
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DesactivarJornada_DebeRetornar409_CuandoLaUsaUnaPlantillaDeTurnos()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await CrearJornadaAsync(LimitesDePrueba.DesactivarJornadaEnPlantillaTurnos, ct);
        var plantillaId = Guid.CreateVersion7();
        (await _client.PostAsJsonAsync("/api/programacion/plantillas-turnos", new
        {
            plantillaId,
            nombre = $"[TEST] Plantilla {plantillaId}",
            diasDelCiclo = 7
        }, ct)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await _client.PutAsJsonAsync($"/api/programacion/plantillas-turnos/{plantillaId}/jornada",
            new { jornadaId = id }, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var reflejada = await Polling.WaitUntilTrueAsync(async () =>
        {
            var cuadro = await _client.GetAsync($"/api/programacion/plantillas-turnos/{plantillaId}", ct);
            return cuadro.IsSuccessStatusCode
                && (await cuadro.Content.ReadAsStringAsync(ct)).Contains(id.ToString(), StringComparison.OrdinalIgnoreCase);
        }, Timeout);
        reflejada.Should().BeTrue("CuadroDeTurnos es una proyeccion Async");

        var response = await _client.PostAsync(RutaDesactivar(id), null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync(ct)).Should().Contain(plantillaId.ToString());
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DesactivarJornada_DebeRetornar404_CuandoLaJornadaNoExiste()
    {
        var response = await _client.PostAsync(
            RutaDesactivar(Guid.CreateVersion7()), null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
