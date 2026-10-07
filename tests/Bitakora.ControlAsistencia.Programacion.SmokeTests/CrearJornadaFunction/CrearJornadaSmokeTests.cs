using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.CrearJornadaFunction;

public class CrearJornadaSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private const string Ruta = "/api/programacion/jornadas";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private readonly HttpClient _client = api.Client;

    private static object Payload(Guid id, int minutos, int topeHoras = 8, int topeMinutos = 0,
        int descansos = 1) => new
        {
            jornadaId = id,
            horasSemanales = new { horas = 30, minutos },
            topeDiario = new { horas = topeHoras, minutos = topeMinutos },
            minimoDiario = new { horas = 0, minutos = 0 },
            diasDescansoPorSemana = descansos
        };

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task HealthCheck_DebeResponder200_CuandoElEntornoEstaDisponible()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearJornada_DebeRetornar201YPersistirJornadaCreada_CuandoPayloadEsValido()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.CreateVersion7();
        var payload = Payload(id, Random.Shared.Next(1, 60));
        var response = await _client.PostAsJsonAsync(Ruta, payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().EndWith($"/programacion/jornadas/{id}");
        (await postgres.ExisteEventoAsync("programacion", id.ToString(), "jornada_creada",
            TimeSpan.FromSeconds(30))).Should().BeTrue();
        (await _client.PostAsJsonAsync(Ruta, payload, ct)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task<JsonElement[]> ListarAsync(CancellationToken ct)
    {
        var response = await _client.GetAsync(Ruta, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return json.RootElement.GetProperty("elementos").EnumerateArray().Select(e => e.Clone()).ToArray();
    }

    private static bool TieneLimites(JsonElement e, int horas, int minutos) =>
        e.GetProperty("horasSemanales").GetProperty("horas").GetInt32() == horas
        && e.GetProperty("horasSemanales").GetProperty("minutos").GetInt32() == minutos
        && e.GetProperty("topeDiario").GetProperty("horas").GetInt32() == 8
        && e.GetProperty("topeDiario").GetProperty("minutos").GetInt32() == 0
        && e.GetProperty("minimoDiario").GetProperty("horas").GetInt32() == 0
        && e.GetProperty("minimoDiario").GetProperty("minutos").GetInt32() == 0
        && e.GetProperty("diasDescansoPorSemana").GetInt32() == 1;

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearJornada_DebeRetornar409_CuandoLosLimitesIgualanAOtraJornada()
    {
        var ct = TestContext.Current.CancellationToken;
        var idA = Guid.CreateVersion7();
        var minutos = Random.Shared.Next(1, 60);
        (await _client.PostAsJsonAsync(Ruta, Payload(idA, minutos), ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        await Polling.WaitUntilTrueAsync(
            async () => (await ListarAsync(ct)).Any(e => e.GetProperty("jornadaId").GetGuid() == idA),
            Timeout);

        var response = await _client.PostAsJsonAsync(Ruta, Payload(Guid.CreateVersion7(), minutos), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync(ct)).Should().Contain(idA.ToString());
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearJornada_DebeRetornar409NombrandoLaExistente_CuandoLosLimitesIgualanALosIniciales()
    {
        var ct = TestContext.Current.CancellationToken;
        var existente = await Polling.WaitUntilAsync(async () =>
        {
            var match = (await ListarAsync(ct)).Where(e => TieneLimites(e, 42, 0)).ToArray();
            return match.Length == 0 ? null : match[0].GetProperty("jornadaId").GetString();
        }, Timeout);

        var payload = new
        {
            jornadaId = Guid.CreateVersion7(),
            horasSemanales = new { horas = 42, minutos = 0 },
            topeDiario = new { horas = 8, minutos = 0 },
            minimoDiario = new { horas = 0, minutos = 0 },
            diasDescansoPorSemana = 1
        };
        var response = await _client.PostAsJsonAsync(Ruta, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync(ct)).Should().Contain(existente!);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearJornada_Retorna201_CuandoIdEsElQueAntesEraLaGeneral()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.Parse("00000000-0000-4000-8000-000000000001");
        await postgres.BorrarStreamsAsync(PostgresFixture.SchemaProgramacion, postgres.TenantId, [id.ToString()]);

        var response = await _client.PostAsJsonAsync(Ruta, Payload(id, 37), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await postgres.ExisteEventoAsync(PostgresFixture.SchemaProgramacion, id.ToString(), "jornada_creada",
            TimeSpan.FromSeconds(30))).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearJornada_DebeRetornar400_CuandoMinutosDelTopeSonInvalidos()
    {
        var response = await _client.PostAsJsonAsync(Ruta,
            Payload(Guid.CreateVersion7(), 23, topeHoras: 0, topeMinutos: 60),
            TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearJornada_DebeRetornar400_CuandoDescansosSonSiete()
    {
        var response = await _client.PostAsJsonAsync(Ruta,
            Payload(Guid.CreateVersion7(), 31, descansos: 7), TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearJornada_DebeRetornar400_CuandoFaltaElTopeDiario()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.PostAsJsonAsync(Ruta, new
        {
            jornadaId = Guid.CreateVersion7(),
            horasSemanales = new { horas = 30, minutos = 17 },
            topeDiario = (object?)null,
            minimoDiario = new { horas = 0, minutos = 0 },
            diasDescansoPorSemana = 1
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
