using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.DesmarcarDescansoAcumuladoDeJornadaFunction;

public class DesmarcarDescansoAcumuladoDeJornadaSmokeTests(ApiFixture api, PostgresFixture postgres) : IDisposable
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

    private async Task<bool> DescansoAcumuladoAsync(Guid id)
    {
        var ct = TestContext.Current.CancellationToken;
        var lectura = await _client.GetAsync($"{Ruta}/{id}", ct);
        lectura.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await lectura.Content.ReadAsStringAsync(ct));
        return json.RootElement.GetProperty("descansoAcumulado").GetBoolean();
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
    public async Task DesmarcarDescansoAcumulado_DebeRetornar204YLimpiarLaMarca_CuandoLaJornadaEstabaMarcada()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await CrearJornadaAsync(LimitesDePrueba.DesmarcarDescansoAcumuladoPersiste);
        (await _client.PutAsync($"{Ruta}/{id}/descanso-acumulado", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await _client.DeleteAsync($"{Ruta}/{id}/descanso-acumulado", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();
        (await DescansoAcumuladoAsync(id)).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DesmarcarDescansoAcumulado_DebeRetornar204SinNuevosEventos_CuandoLaJornadaNoEstaMarcada()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");
        var ct = TestContext.Current.CancellationToken;
        var id = await CrearJornadaAsync(LimitesDePrueba.DesmarcarDescansoAcumuladoSinMarca);
        var lineaBase = await postgres.ContarEventosDeStreamAsync(
            PostgresFixture.SchemaProgramacion, _tenant.TenantId, id.ToString());

        var response = await _client.DeleteAsync($"{Ruta}/{id}/descanso-acumulado", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await postgres.ContarEventosDeStreamAsync(
            PostgresFixture.SchemaProgramacion, _tenant.TenantId, id.ToString()))
            .Should().Be(lineaBase);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DesmarcarDescansoAcumulado_DebeRetornar404_CuandoLaJornadaNoExiste()
    {
        var response = await _client.DeleteAsync($"{Ruta}/{Guid.CreateVersion7()}/descanso-acumulado",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DesmarcarDescansoAcumulado_DebeRetornar400_CuandoElIdNoEsGuid()
    {
        var response = await _client.DeleteAsync($"{Ruta}/no-es-guid/descanso-acumulado",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
