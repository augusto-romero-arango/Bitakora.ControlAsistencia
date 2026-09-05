using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.AsignarTurnoADiaDePlantillaSemanalFunction;

// Issue #621 CA-8: PUT que reemplaza el turno de un dia de la plantilla semanal. mt_events es la
// unica ventana black-box al efecto secundario del handler (evento interno, no cruza bus).
public class AsignarTurnoADiaDePlantillaSemanalSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private const string SchemaProgramacion = "programacion";
    private const string TipoEventoDiaAsignado = "dia_de_plantilla_semanal_asignado";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _client = api.Client;

    private static object PayloadPlantillaValida(Guid plantillaId, int semanas = 2) => new
    {
        plantillaId,
        nombre = $"[TEST] Plantilla {plantillaId}",
        semanas
    };

    private static object PayloadTurnoValido(Guid turnoId) => new
    {
        turnoId,
        nombre = $"[TEST] Turno {turnoId}",
        ordinarias = new[]
        {
            new
            {
                inicio = "06:00:00",
                fin = "14:00:00",
                descansos = Array.Empty<object>(),
                extras = Array.Empty<object>()
            }
        }
    };

    // El arrange usa IsSuccessStatusCode (no un codigo especifico) para CrearTurno: hoy responde
    // 202, pero #640 lo migrara a un codigo de exito con transaccion confirmada -- este smoke test
    // no debe acoplarse a ese detalle en transicion.
    private async Task<(Guid PlantillaId, Guid TurnoId)> CrearPlantillaYTurnoAsync(CancellationToken ct)
    {
        var plantillaId = Guid.CreateVersion7();
        var turnoId = Guid.CreateVersion7();

        var plantillaResponse = await _client.PostAsJsonAsync(
            "/api/programacion/plantillas-semanales", PayloadPlantillaValida(plantillaId), ct);
        plantillaResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que CrearPlantillaSemanal funcione");

        var turnoResponse = await _client.PostAsJsonAsync(
            "/api/programacion/turnos", PayloadTurnoValido(turnoId), ct);
        turnoResponse.IsSuccessStatusCode.Should().BeTrue(
            "el arrange de este smoke test depende de que CrearTurno acepte el comando");

        return (plantillaId, turnoId);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task HealthCheck_DebeResponder200()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarTurnoADia_DebeRetornar204YPersistirElDia_CuandoLaPlantillaYElTurnoExisten()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var (plantillaId, turnoId) = await CrearPlantillaYTurnoAsync(ct);

        var response = await _client.PutAsJsonAsync(
            $"/api/programacion/plantillas-semanales/{plantillaId}/dias/1/5", new { turnoId }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var streamId = plantillaId.ToString();
        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaProgramacion, streamId, TipoEventoDiaAsignado,
            campoJson: "TurnoId", valorJson: turnoId.ToString(), Timeout);

        eventoPersistido.GetProperty("Semana").GetInt32().Should().Be(1);
        eventoPersistido.GetProperty("Dia").GetInt32().Should().Be(5);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarTurnoADia_DebeRetornar409_CuandoLaSemanaSuperaElTotalDeLaPlantilla()
    {
        var ct = TestContext.Current.CancellationToken;
        var (plantillaId, turnoId) = await CrearPlantillaYTurnoAsync(ct);

        var response = await _client.PutAsJsonAsync(
            $"/api/programacion/plantillas-semanales/{plantillaId}/dias/3/5", new { turnoId }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarTurnoADia_DebeRetornar400_CuandoElDiaEstaFueraDeLaSemanaIso()
    {
        var ct = TestContext.Current.CancellationToken;
        var (plantillaId, turnoId) = await CrearPlantillaYTurnoAsync(ct);

        var response = await _client.PutAsJsonAsync(
            $"/api/programacion/plantillas-semanales/{plantillaId}/dias/1/8", new { turnoId }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarTurnoADia_DebeRetornar404_CuandoElTurnoNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var plantillaId = Guid.CreateVersion7();

        var plantillaResponse = await _client.PostAsJsonAsync(
            "/api/programacion/plantillas-semanales", PayloadPlantillaValida(plantillaId), ct);
        plantillaResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que CrearPlantillaSemanal funcione");

        var response = await _client.PutAsJsonAsync(
            $"/api/programacion/plantillas-semanales/{plantillaId}/dias/1/5",
            new { turnoId = Guid.CreateVersion7() }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
