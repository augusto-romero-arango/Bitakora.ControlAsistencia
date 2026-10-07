using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.AsignarJornadaAPlantillaSemanalFunction;

public class AsignarJornadaAPlantillaSemanalSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private const string TipoEventoAsignada = "jornada_de_plantilla_semanal_asignada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _client = api.Client;

    private async Task<(Guid PlantillaId, Guid JornadaId, int Minutos)> CrearPlantillaYJornadaAsync(CancellationToken ct)
    {
        var plantillaId = Guid.CreateVersion7();
        var jornadaId = Guid.CreateVersion7();
        var minutos = Random.Shared.Next(1, 60);

        (await _client.PostAsJsonAsync("/api/programacion/plantillas-semanales", new
        {
            plantillaId,
            nombre = $"[TEST] Plantilla {plantillaId}",
            semanas = 1
        }, ct)).StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange depende de que CrearPlantillaSemanal funcione");
        (await _client.PostAsJsonAsync("/api/programacion/jornadas", new
        {
            jornadaId,
            horasSemanales = new { horas = 30, minutos },
            topeDiario = new { horas = 8, minutos = 0 },
            minimoDiario = new { horas = 0, minutos = 0 },
            diasDescansoPorSemana = 1
        }, ct)).StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange depende de que CrearJornada funcione");

        return (plantillaId, jornadaId, minutos);
    }

    private static string Ruta(Guid plantillaId) =>
        $"/api/programacion/plantillas-semanales/{plantillaId}/jornada";

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task HealthCheck_DebeResponder200()
    {
        var response = await _client.GetAsync("/api/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarJornada_DebeRetornar204YPersistirElEvento_CuandoLaJornadaExiste()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var (plantillaId, jornadaId, _) = await CrearPlantillaYJornadaAsync(ct);

        var response = await _client.PutAsJsonAsync(Ruta(plantillaId), new { jornadaId }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var existe = await postgres.ExisteEventoAsync(
            PostgresFixture.SchemaProgramacion, plantillaId.ToString(), TipoEventoAsignada, Timeout);
        existe.Should().BeTrue(
            $"el evento {TipoEventoAsignada} deberia existir en el stream {plantillaId}");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarJornada_DebeRetornar204SinNuevosEventos_CuandoSeRepiteConLaCopiaAlDia()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var (plantillaId, jornadaId, _) = await CrearPlantillaYJornadaAsync(ct);
        var streamId = plantillaId.ToString();

        (await _client.PutAsJsonAsync(Ruta(plantillaId), new { jornadaId }, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var eventosTrasPrimera = await postgres.ContarEventosDeStreamAsync(
            PostgresFixture.SchemaProgramacion, postgres.TenantId, streamId);

        var response = await _client.PutAsJsonAsync(Ruta(plantillaId), new { jornadaId }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var eventosTrasSegunda = await postgres.ContarEventosDeStreamAsync(
            PostgresFixture.SchemaProgramacion, postgres.TenantId, streamId);
        eventosTrasSegunda.Should().Be(eventosTrasPrimera);
    }

    // Guarda en dev del supuesto no verificado del issue: si GetAggregateRootAsync no llenara
    // AggregateRoot.Version, la copia quedaria en 0 y la autocorreccion nunca se dispararia.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarJornada_DebeAutocorregirLaCopia_CuandoLosLimitesDeLaJornadaCambiaron()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var (plantillaId, jornadaId, minutos) = await CrearPlantillaYJornadaAsync(ct);
        (await _client.PutAsJsonAsync(Ruta(plantillaId), new { jornadaId }, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.PutAsJsonAsync($"/api/programacion/jornadas/{jornadaId}/limites", new
        {
            horasSemanales = new { horas = 33, minutos },
            topeDiario = new { horas = 8, minutos = 0 },
            minimoDiario = new { horas = 0, minutos = 0 },
            diasDescansoPorSemana = 1
        }, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange depende de que ModificarLimitesJornada funcione");

        var response = await _client.PutAsJsonAsync(Ruta(plantillaId), new { jornadaId }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var copias = (await postgres.LeerEventosDeStreamAsync(
                PostgresFixture.SchemaProgramacion, postgres.TenantId, plantillaId.ToString()))
            .Where(e => e.TryGetProperty("VersionJornada", out _))
            .ToList();
        copias.Should().HaveCount(2, "la reasignacion con la copia atrasada emite la autocorreccion");
        var versionInicial = copias[0].GetProperty("VersionJornada").GetInt64();
        versionInicial.Should().BeGreaterThan(0, "la copia guarda la version real del stream de la Jornada");
        copias[1].GetProperty("VersionJornada").GetInt64().Should().BeGreaterThan(versionInicial);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarJornada_DebeRetornar404_CuandoLaJornadaNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var (plantillaId, _, _) = await CrearPlantillaYJornadaAsync(ct);

        var response = await _client.PutAsJsonAsync(
            Ruta(plantillaId), new { jornadaId = Guid.CreateVersion7() }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarJornada_DebeRetornar404_CuandoLaPlantillaNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, jornadaId, _) = await CrearPlantillaYJornadaAsync(ct);

        var response = await _client.PutAsJsonAsync(Ruta(Guid.CreateVersion7()), new { jornadaId }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarJornada_DebeRetornar409_CuandoLaPlantillaEstaRetirada()
    {
        var ct = TestContext.Current.CancellationToken;
        var (plantillaId, jornadaId, _) = await CrearPlantillaYJornadaAsync(ct);

        (await _client.DeleteAsync($"/api/programacion/plantillas-semanales/{plantillaId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent,
                "el arrange depende de que el retiro de la plantilla funcione");

        var response = await _client.PutAsJsonAsync(Ruta(plantillaId), new { jornadaId }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
