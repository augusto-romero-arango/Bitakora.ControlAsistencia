using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.QuitarFranjaFunction;

public class QuitarFranjaSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaTurnos = "/api/programacion/turnos";
    private const string SchemaProgramacion = "programacion";
    private const string TipoEventoFranjaQuitada = "franja_quitada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static string RutaAgregarFranja(Guid turnoId) => $"{RutaTurnos}/{turnoId}:agregar-franja";
    private static string RutaQuitarFranja(Guid turnoId) => $"{RutaTurnos}/{turnoId}:quitar-franja";

    private async Task CrearTurnoVacioAsync(Guid turnoId, string nombreBase, CancellationToken ct)
    {
        var payload = new { turnoId, nombre = $"{nombreBase} {turnoId}" };
        var response = await _client.PostAsJsonAsync(RutaTurnos, payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que CrearTurno funcione");
    }

    private async Task AgregarFranjaAsync(Guid turnoId, CancellationToken ct)
    {
        var payload = new
        {
            inicio = "15:00:00",
            fin = "19:00:00",
            sede = new { id = "SEDE-SUBA", nombre = "[TEST] Suba" }
        };
        var response = await _client.PostAsJsonAsync(RutaAgregarFranja(turnoId), payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que AgregarFranja funcione");
    }

    // franja_quitada no cruza ningun bus: mt_events es la unica ventana black-box a lo que quedo
    // grabado. El segundo :quitar-franja sobre la misma hora cierra la regla de negocio -> 409.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarFranja_DebeRetornar202YPersistirLaFranjaQuitada_CuandoLaFranjaExiste()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        await CrearTurnoVacioAsync(turnoId, "[TEST] Turno Para Quitar Franja", ct);
        await AgregarFranjaAsync(turnoId, ct);

        var payload = new { franja = "15:00" };
        var response = await _client.PostAsJsonAsync(RutaQuitarFranja(turnoId), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = turnoId.ToString();
        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaProgramacion, streamId, TipoEventoFranjaQuitada,
            campoJson: "TurnoId", valorJson: turnoId.ToString(), Timeout);

        var franja = eventoPersistido.GetProperty("Franja");
        franja.GetProperty("horaInicio").GetString().Should().Be("15:00:00");
        EventoPersistido.SedeDe(franja).Should().Be(new SedeMinima("SEDE-SUBA", "[TEST] Suba"));

        var segundaRespuesta = await _client.PostAsJsonAsync(RutaQuitarFranja(turnoId), payload, ct);

        segundaRespuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarFranja_DebeRetornar404_CuandoElTurnoNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new { franja = "15:00" };

        var response = await _client.PostAsJsonAsync(
            RutaQuitarFranja(Guid.CreateVersion7()), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarFranja_DebeRetornar400_CuandoElIdDeRutaNoEsUnGuidValido()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new { franja = "15:00" };

        var response = await _client.PostAsJsonAsync(
            $"{RutaTurnos}/no-es-un-guid:quitar-franja", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
