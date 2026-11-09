using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.AsignarModalidadAJornadaFunction;

public class AsignarModalidadAJornadaSmokeTests(ApiFixture api, PostgresFixture postgres) : IDisposable
{
    private readonly TenantDeTest _tenant = new(api);

    public void Dispose() => _tenant.Dispose();

    private const string Ruta = "/api/programacion/jornadas";
    private HttpClient _client => _tenant.Client;

    private async Task<Guid> CrearJornadaAsync(FirmaDeLimites firma)
    {
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.CreateVersion7();
        (await _client.PostAsJsonAsync(Ruta, firma.PayloadCrear(id), ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        return id;
    }

    private async Task<JsonElement> LeerJornadaAsync(Guid id)
    {
        var ct = TestContext.Current.CancellationToken;
        var lectura = await _client.GetAsync($"{Ruta}/{id}", ct);
        lectura.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await lectura.Content.ReadAsStringAsync(ct));
        return json.RootElement.Clone();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task HealthCheck_DebeResponder200_CuandoElEntornoEstaDisponible()
    {
        var response = await _client.GetAsync("/api/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarModalidad_DebeRetornar204YExponerlaEnLaJornada_CuandoLaModalidadEsValida()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await CrearJornadaAsync(LimitesDePrueba.AsignarModalidadPersiste);

        var response = await _client.PutAsJsonAsync($"{Ruta}/{id}/modalidad", new { modalidad = "TurnosSucesivos" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();
        var jornada = await LeerJornadaAsync(id);
        jornada.GetProperty("modalidad").ValueKind.Should().Be(JsonValueKind.String);
        jornada.GetProperty("descripcion").GetString().Should().Contain("Turnos sucesivos");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarModalidad_DebeReemplazarLaModalidad_CuandoLaJornadaTeniaOtra()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await CrearJornadaAsync(LimitesDePrueba.AsignarModalidadReemplaza);
        (await _client.PutAsJsonAsync($"{Ruta}/{id}/modalidad", new { modalidad = "Flexible" }, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await _client.PutAsJsonAsync($"{Ruta}/{id}/modalidad", new { modalidad = "Ordinaria" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var jornada = await LeerJornadaAsync(id);
        jornada.GetProperty("descripcion").GetString().Should().Contain("Ordinaria").And.NotContain("Flexible");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarModalidad_DebeRetornar204SinNuevosEventos_CuandoLaJornadaYaTieneEsaModalidad()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");
        var ct = TestContext.Current.CancellationToken;
        var id = await CrearJornadaAsync(LimitesDePrueba.AsignarModalidadRepite);
        var body = new { modalidad = "SinSolucionDeContinuidad" };
        (await _client.PutAsJsonAsync($"{Ruta}/{id}/modalidad", body, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var lineaBase = await postgres.ContarEventosDeStreamAsync(
            PostgresFixture.SchemaProgramacion, _tenant.TenantId, id.ToString());

        var repetida = await _client.PutAsJsonAsync($"{Ruta}/{id}/modalidad", body, ct);

        repetida.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await postgres.ContarEventosDeStreamAsync(
            PostgresFixture.SchemaProgramacion, _tenant.TenantId, id.ToString()))
            .Should().Be(lineaBase);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarModalidad_DebeRetornar404_CuandoLaJornadaNoExiste()
    {
        var response = await _client.PutAsJsonAsync($"{Ruta}/{Guid.CreateVersion7()}/modalidad",
            new { modalidad = "Ordinaria" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarModalidad_DebeRetornar400_CuandoLaModalidadNoEsReconocida()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await CrearJornadaAsync(LimitesDePrueba.AsignarModalidadNoReconocida);

        var response = await _client.PutAsJsonAsync($"{Ruta}/{id}/modalidad", new { modalidad = "[TEST] Inexistente" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarModalidad_DebeRetornar400_CuandoElIdNoEsGuid()
    {
        var response = await _client.PutAsJsonAsync($"{Ruta}/no-es-guid/modalidad",
            new { modalidad = "Ordinaria" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
