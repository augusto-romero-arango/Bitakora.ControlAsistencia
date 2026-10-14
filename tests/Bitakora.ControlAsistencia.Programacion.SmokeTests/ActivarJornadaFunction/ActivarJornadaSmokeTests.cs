using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.ActivarJornadaFunction;

public class ActivarJornadaSmokeTests(ApiFixture api, PostgresFixture postgres) : IDisposable
{
    private readonly TenantDeTest _tenant = new(api);

    public void Dispose() => _tenant.Dispose();

    private const string RutaJornadas = "/api/programacion/jornadas";
    private const string TipoEvento = "jornada_activada";
    private HttpClient _client => _tenant.Client;

    private static string RutaActivar(Guid id) => $"{RutaJornadas}/{id}:activar";

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task HealthCheck_Responde200()
    {
        var response = await _client.GetAsync("/api/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ActivarJornada_Retorna204YDejaActiva_CuandoEstabaDesactivada()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.CreateVersion7();
        (await _client.PostAsJsonAsync(RutaJornadas, LimitesDePrueba.ActivarJornadaPersiste.PayloadCrear(id), ct))
            .StatusCode.Should().Be(HttpStatusCode.Created, "el arrange depende de que CrearJornada funcione");
        (await _client.PostAsync($"{RutaJornadas}/{id}:desactivar", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent, "el arrange depende de que DesactivarJornada funcione");

        var activada = await _client.PostAsync(RutaActivar(id), null, ct);

        activada.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await activada.Content.ReadAsStringAsync(ct)).Should().BeEmpty();
        var obtenida = await _client.GetAsync($"{RutaJornadas}/{id}", ct);
        obtenida.StatusCode.Should().Be(HttpStatusCode.OK);
        (await obtenida.Content.ReadFromJsonAsync<JsonElement>(ct))
            .GetProperty("activa").GetBoolean().Should().BeTrue();

        var repetida = await _client.PostAsync(RutaActivar(id), null, ct);

        repetida.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await postgres.ContarEventosAsync(PostgresFixture.SchemaProgramacion, id.ToString(), TipoEvento))
            .Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ActivarJornada_Retorna404_CuandoLaJornadaNoExiste()
    {
        var response = await _client.PostAsync(
            RutaActivar(Guid.CreateVersion7()), null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ActivarJornada_Retorna400_CuandoElIdNoEsGuid()
    {
        var response = await _client.PostAsync(
            $"{RutaJornadas}/no-es-un-guid:activar", null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
