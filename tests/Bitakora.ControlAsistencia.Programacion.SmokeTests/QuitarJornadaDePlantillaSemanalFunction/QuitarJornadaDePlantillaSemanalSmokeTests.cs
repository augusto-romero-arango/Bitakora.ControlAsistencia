using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.QuitarJornadaDePlantillaSemanalFunction;

public class QuitarJornadaDePlantillaSemanalSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private const string TipoEventoQuitada = "jornada_de_plantilla_semanal_quitada";
    private const string TipoEventoAdvertencias = "advertencias_de_plantilla_semanal_calculadas";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _client = api.Client;

    private static string Ruta(Guid plantillaId) =>
        $"/api/programacion/plantillas-semanales/{plantillaId}/jornada";

    private async Task<Guid> CrearPlantillaAsync(CancellationToken ct)
    {
        var plantillaId = Guid.CreateVersion7();
        (await _client.PostAsJsonAsync("/api/programacion/plantillas-semanales", new
        {
            plantillaId,
            nombre = $"[TEST] Plantilla {plantillaId}",
            semanas = 1
        }, ct)).StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange depende de que CrearPlantillaSemanal funcione");
        return plantillaId;
    }

    private async Task<Guid> CrearPlantillaConJornadaAsync(CancellationToken ct)
    {
        var plantillaId = await CrearPlantillaAsync(ct);
        var jornadaId = Guid.CreateVersion7();

        (await _client.PostAsJsonAsync("/api/programacion/jornadas", new
        {
            jornadaId,
            horasSemanales = new { horas = 30, minutos = Random.Shared.Next(1, 60) },
            topeDiario = new { horas = 8, minutos = 0 },
            minimoDiario = new { horas = 0, minutos = 0 },
            diasDescansoPorSemana = 1
        }, ct)).StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange depende de que CrearJornada funcione");
        (await _client.PutAsJsonAsync(Ruta(plantillaId), new { jornadaId }, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent,
                "el arrange depende de que AsignarJornadaAPlantillaSemanal funcione");

        return plantillaId;
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task HealthCheck_DebeResponder200()
    {
        var response = await _client.GetAsync("/api/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarJornada_DebeRetornar204YPersistirElEvento_CuandoLaPlantillaTieneJornada()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var plantillaId = await CrearPlantillaConJornadaAsync(ct);

        var response = await _client.DeleteAsync(Ruta(plantillaId), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var existe = await postgres.ExisteEventoAsync(
            PostgresFixture.SchemaProgramacion, plantillaId.ToString(), TipoEventoQuitada, Timeout);
        existe.Should().BeTrue(
            $"el evento {TipoEventoQuitada} deberia existir en el stream {plantillaId}");
        (await postgres.ContarEventosAsync(
            PostgresFixture.SchemaProgramacion, plantillaId.ToString(), TipoEventoAdvertencias))
            .Should().Be(3, "quitar la Jornada re-audita la plantilla: crear, asignar y quitar auditan cada uno");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarJornada_DebeRetornar204SinNuevosEventos_CuandoLaPlantillaNoTieneJornada()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var plantillaId = await CrearPlantillaConJornadaAsync(ct);
        var streamId = plantillaId.ToString();

        (await _client.DeleteAsync(Ruta(plantillaId), ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var eventosTrasPrimera = await postgres.ContarEventosDeStreamAsync(
            PostgresFixture.SchemaProgramacion, postgres.TenantId, streamId);

        var response = await _client.DeleteAsync(Ruta(plantillaId), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var eventosTrasSegunda = await postgres.ContarEventosDeStreamAsync(
            PostgresFixture.SchemaProgramacion, postgres.TenantId, streamId);
        eventosTrasSegunda.Should().Be(eventosTrasPrimera);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarJornada_DebeRetornar404_CuandoLaPlantillaNoExiste()
    {
        var response = await _client.DeleteAsync(
            Ruta(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarJornada_DebeRetornar409_CuandoLaPlantillaEstaRetirada()
    {
        var ct = TestContext.Current.CancellationToken;
        var plantillaId = await CrearPlantillaAsync(ct);

        (await _client.DeleteAsync($"/api/programacion/plantillas-semanales/{plantillaId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent,
                "el arrange depende de que el retiro de la plantilla funcione");

        var response = await _client.DeleteAsync(Ruta(plantillaId), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
