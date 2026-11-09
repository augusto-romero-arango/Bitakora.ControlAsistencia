using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.QuitarModalidadDeJornadaFunction;

public class QuitarModalidadDeJornadaSmokeTests(ApiFixture api, PostgresFixture postgres) : IDisposable
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

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task HealthCheck_DebeResponder200_CuandoElEntornoEstaDisponible()
    {
        var response = await _client.GetAsync("/api/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarModalidad_DebeRetornar204YLimpiarLaModalidad_CuandoLaJornadaTeniaModalidad()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await CrearJornadaAsync(LimitesDePrueba.QuitarModalidadPersiste);
        (await _client.PutAsJsonAsync($"{Ruta}/{id}/modalidad", new { modalidad = "Flexible" }, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await _client.DeleteAsync($"{Ruta}/{id}/modalidad", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();
        var lectura = await _client.GetAsync($"{Ruta}/{id}", ct);
        lectura.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await lectura.Content.ReadAsStringAsync(ct));
        json.RootElement.GetProperty("modalidad").ValueKind.Should().Be(JsonValueKind.Null);
        json.RootElement.GetProperty("descripcion").GetString().Should().NotContain("modalidad");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarModalidad_DebeRetornar204SinNuevosEventos_CuandoLaJornadaYaNoTieneModalidad()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");
        var ct = TestContext.Current.CancellationToken;
        var id = await CrearJornadaAsync(LimitesDePrueba.QuitarModalidadSinModalidad);
        var lineaBase = await postgres.ContarEventosDeStreamAsync(
            PostgresFixture.SchemaProgramacion, _tenant.TenantId, id.ToString());

        var response = await _client.DeleteAsync($"{Ruta}/{id}/modalidad", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await postgres.ContarEventosDeStreamAsync(
            PostgresFixture.SchemaProgramacion, _tenant.TenantId, id.ToString()))
            .Should().Be(lineaBase);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarModalidad_DebeRetornar404_CuandoLaJornadaNoExiste()
    {
        var response = await _client.DeleteAsync($"{Ruta}/{Guid.CreateVersion7()}/modalidad",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarModalidad_DebeRetornar400_CuandoElIdNoEsGuid()
    {
        var response = await _client.DeleteAsync($"{Ruta}/no-es-guid/modalidad",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
