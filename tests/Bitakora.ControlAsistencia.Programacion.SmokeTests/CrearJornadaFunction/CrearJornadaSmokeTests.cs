using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.CrearJornadaFunction;

public class CrearJornadaSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private const string Ruta = "/api/programacion/jornadas";
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

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearJornada_DebeRetornar201_CuandoIdEsElQueAntesEraLaGeneral()
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
