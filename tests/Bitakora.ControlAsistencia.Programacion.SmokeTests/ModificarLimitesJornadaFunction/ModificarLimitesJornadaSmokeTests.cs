using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.ModificarLimitesJornadaFunction;

public class ModificarLimitesJornadaSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private const string Ruta = "/api/programacion/jornadas";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private readonly HttpClient _client = api.Client;

    private static object Limites(int horas, int minutos, int topeHoras = 8, int topeMinutos = 0) => new
    {
        horasSemanales = new { horas, minutos },
        topeDiario = new { horas = topeHoras, minutos = topeMinutos },
        minimoDiario = new { horas = 0, minutos = 0 },
        diasDescansoPorSemana = 1
    };

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task HealthCheck_DebeResponder200_CuandoElEntornoEstaDisponible()
    {
        var response = await _client.GetAsync("/api/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ModificarLimitesJornada_DebeRetornar204YPersistirLosLimites_CuandoPayloadEsValido()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.CreateVersion7();
        var minutos = Random.Shared.Next(1, 60);
        var creada = await _client.PostAsJsonAsync(Ruta, new
        {
            jornadaId = id,
            horasSemanales = new { horas = 30, minutos },
            topeDiario = new { horas = 8, minutos = 0 },
            minimoDiario = new { horas = 0, minutos = 0 },
            diasDescansoPorSemana = 1
        }, ct);
        creada.StatusCode.Should().Be(HttpStatusCode.Created);
        var destino = Limites(31, minutos);

        var modificada = await _client.PutAsJsonAsync($"{Ruta}/{id}/limites", destino, ct);

        modificada.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var lectura = await _client.GetAsync($"{Ruta}/{id}", ct);
        lectura.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await lectura.Content.ReadAsStringAsync(ct));
        var semanales = json.RootElement.GetProperty("horasSemanales");
        semanales.GetProperty("horas").GetInt32().Should().Be(31);
        semanales.GetProperty("minutos").GetInt32().Should().Be(minutos);

        (await _client.PutAsJsonAsync($"{Ruta}/{id}/limites", destino, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await postgres.ContarEventosAsync("programacion", id.ToString(), "limites_jornada_modificados"))
            .Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ModificarLimitesJornada_DebeRetornar409_CuandoLosLimitesIgualanAOtraJornada()
    {
        var ct = TestContext.Current.CancellationToken;
        var idA = Guid.CreateVersion7();
        var idB = Guid.CreateVersion7();
        var minutos = Random.Shared.Next(1, 60);
        (await _client.PostAsJsonAsync(Ruta, CrearPayload(idA, 30, minutos), ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await _client.PostAsJsonAsync(Ruta, CrearPayload(idB, 31, minutos), ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        await Polling.WaitUntilTrueAsync(async () =>
        {
            var response = await _client.GetAsync(Ruta, ct);
            return (await response.Content.ReadAsStringAsync(ct)).Contains(idA.ToString());
        }, Timeout);

        var conflicto = await _client.PutAsJsonAsync($"{Ruta}/{idB}/limites", Limites(30, minutos), ct);

        conflicto.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await conflicto.Content.ReadAsStringAsync(ct)).Should().Contain(idA.ToString());
        (await _client.PutAsJsonAsync($"{Ruta}/{idA}/limites", Limites(30, minutos), ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static object CrearPayload(Guid id, int horas, int minutos) => new
    {
        jornadaId = id,
        horasSemanales = new { horas, minutos },
        topeDiario = new { horas = 8, minutos = 0 },
        minimoDiario = new { horas = 0, minutos = 0 },
        diasDescansoPorSemana = 1
    };

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ModificarLimitesJornada_DebeRetornar404_CuandoLaJornadaNoExiste()
    {
        var response = await _client.PutAsJsonAsync($"{Ruta}/{Guid.CreateVersion7()}/limites",
            Limites(31, 17), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ModificarLimitesJornada_DebeRetornar400_CuandoElIdNoEsGuid()
    {
        var response = await _client.PutAsJsonAsync($"{Ruta}/no-es-guid/limites",
            Limites(31, 17), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ModificarLimitesJornada_DebeRetornar400_CuandoElTopeDiarioEsInvalido()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.CreateVersion7();
        var minutos = Random.Shared.Next(1, 60);
        (await _client.PostAsJsonAsync(Ruta, new
        {
            jornadaId = id,
            horasSemanales = new { horas = 30, minutos },
            topeDiario = new { horas = 8, minutos = 0 },
            minimoDiario = new { horas = 0, minutos = 0 },
            diasDescansoPorSemana = 1
        }, ct)).StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await _client.PutAsJsonAsync($"{Ruta}/{id}/limites",
            Limites(31, minutos, topeHoras: 0, topeMinutos: 60), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
