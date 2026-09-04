using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.AgregarFranjaFunction;

public class AgregarFranjaSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaTurnos = "/api/programacion/turnos";
    private const string SchemaProgramacion = "programacion";
    private const string TipoEventoFranjaAgregada = "franja_agregada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static string RutaAgregarFranja(Guid turnoId) => $"{RutaTurnos}/{turnoId}:agregar-franja";

    private async Task CrearTurnoVacioAsync(Guid turnoId, string nombreBase, CancellationToken ct)
    {
        var payload = new { turnoId, nombre = $"{nombreBase} {turnoId}" };
        var response = await _client.PostAsJsonAsync(RutaTurnos, payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que CrearTurno funcione");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task HealthCheck_DebeResponder200()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // CA-8: primer paso del diseno de turno por pasos -- crear el turno vacio y agregarle una
    // franja ordinaria con sede prearmada. franja_agregada no cruza ningun bus: mt_events es la
    // unica ventana black-box a lo que quedo grabado. El segundo POST con una franja que se
    // solapa (23:00-01:00 cae dentro de 22:00-06:00+1) cierra la regla de negocio -> 409.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AgregarFranja_DebeRetornar202YPersistirLaFranja_CuandoElTurnoEstaVacio()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        await CrearTurnoVacioAsync(turnoId, "[TEST] Turno Por Pasos", ct);

        var payload = new
        {
            inicio = "22:00:00",
            fin = "06:00:00",
            sede = new { id = "SEDE-SUBA", nombre = "[TEST] Suba" }
        };
        var response = await _client.PostAsJsonAsync(RutaAgregarFranja(turnoId), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = turnoId.ToString();
        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaProgramacion, streamId, TipoEventoFranjaAgregada,
            campoJson: "TurnoId", valorJson: turnoId.ToString(), Timeout);

        var franja = eventoPersistido.GetProperty("Franja");
        franja.GetProperty("horaInicio").GetString().Should().Be("22:00:00");
        franja.GetProperty("diaOffsetFin").GetInt32().Should().Be(1);
        franja.GetProperty("sede").GetProperty("id").GetString().Should().Be("SEDE-SUBA");

        var payloadSolapada = new { inicio = "23:00:00", fin = "01:00:00" };
        var segundaRespuesta = await _client.PostAsJsonAsync(
            RutaAgregarFranja(turnoId), payloadSolapada, ct);

        segundaRespuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
