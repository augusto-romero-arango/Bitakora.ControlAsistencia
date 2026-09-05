using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.QuitarSubFranjaFunction;

public class QuitarSubFranjaSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaTurnos = "/api/programacion/turnos";
    private const string SchemaProgramacion = "programacion";
    private const string TipoEventoDescansoQuitado = "descanso_quitado";
    private const string TipoEventoExtraQuitado = "extra_quitado";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static string RutaAgregarFranja(Guid turnoId) => $"{RutaTurnos}/{turnoId}:agregar-franja";
    private static string RutaAgregarSubFranja(Guid turnoId) => $"{RutaTurnos}/{turnoId}:agregar-subfranja";
    private static string RutaQuitarSubFranja(Guid turnoId) => $"{RutaTurnos}/{turnoId}:quitar-subfranja";

    private async Task CrearTurnoVacioAsync(Guid turnoId, string nombreBase, CancellationToken ct)
    {
        var payload = new { turnoId, nombre = $"{nombreBase} {turnoId}" };
        var response = await _client.PostAsJsonAsync(RutaTurnos, payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que CrearTurno funcione");
    }

    private async Task AgregarFranjaNocturnaAsync(Guid turnoId, CancellationToken ct)
    {
        var payload = new { inicio = "22:00:00", fin = "06:00:00" };
        var response = await _client.PostAsJsonAsync(RutaAgregarFranja(turnoId), payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que AgregarFranja funcione");
    }

    private async Task AgregarDescansoDeMadrugadaAsync(Guid turnoId, CancellationToken ct)
    {
        var payload = new { franja = "22:00", tipo = "descanso", inicio = "02:00", fin = "02:30" };
        var response = await _client.PostAsJsonAsync(RutaAgregarSubFranja(turnoId), payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que AgregarSubFranja funcione");
    }

    private async Task AgregarExtraDeMadrugadaAsync(Guid turnoId, CancellationToken ct)
    {
        var payload = new { franja = "22:00", tipo = "extra", inicio = "05:00", fin = "06:00" };
        var response = await _client.PostAsJsonAsync(RutaAgregarSubFranja(turnoId), payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que AgregarSubFranja funcione");
    }

    // descanso_quitado no cruza ningun bus: mt_events es la unica ventana black-box a lo que
    // quedo grabado. El segundo :quitar-subfranja sobre la misma hija cierra la regla de negocio
    // -> 409.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarSubFranja_DebeRetornar202YPersistirLaFranjaSinElDescanso_CuandoElDescansoExiste()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        await CrearTurnoVacioAsync(turnoId, "[TEST] Turno Quitar Subfranja", ct);
        await AgregarFranjaNocturnaAsync(turnoId, ct);
        await AgregarDescansoDeMadrugadaAsync(turnoId, ct);

        var payload = new { franja = "22:00", tipo = "descanso", inicio = "02:00" };
        var response = await _client.PostAsJsonAsync(RutaQuitarSubFranja(turnoId), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = turnoId.ToString();
        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaProgramacion, streamId, TipoEventoDescansoQuitado,
            campoJson: "TurnoId", valorJson: turnoId.ToString(), Timeout);

        eventoPersistido.GetProperty("Franja").GetProperty("descansos").GetArrayLength()
            .Should().Be(0);

        var segundaRespuesta = await _client.PostAsJsonAsync(RutaQuitarSubFranja(turnoId), payload, ct);

        segundaRespuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // El test de descanso ya cubre el camino feliz del comando; este solo cierra que el otro
    // valor del discriminador enruta al evento gemelo extra_quitado.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarSubFranja_DebeRetornar202YPersistirLaFranjaSinElExtra_CuandoTipoEsExtra()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        await CrearTurnoVacioAsync(turnoId, "[TEST] Turno Quitar Subfranja Extra", ct);
        await AgregarFranjaNocturnaAsync(turnoId, ct);
        await AgregarExtraDeMadrugadaAsync(turnoId, ct);

        var payload = new { franja = "22:00", tipo = "extra", inicio = "05:00" };
        var response = await _client.PostAsJsonAsync(RutaQuitarSubFranja(turnoId), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = turnoId.ToString();
        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaProgramacion, streamId, TipoEventoExtraQuitado,
            campoJson: "TurnoId", valorJson: turnoId.ToString(), Timeout);

        eventoPersistido.GetProperty("Franja").GetProperty("extras").GetArrayLength()
            .Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarSubFranja_DebeRetornar404_CuandoElTurnoNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new { franja = "22:00", tipo = "descanso", inicio = "02:00" };

        var response = await _client.PostAsJsonAsync(
            RutaQuitarSubFranja(Guid.CreateVersion7()), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarSubFranja_DebeRetornar400_CuandoElTipoEsDesconocido()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new { franja = "22:00", tipo = "pausa", inicio = "02:00" };

        var response = await _client.PostAsJsonAsync(
            RutaQuitarSubFranja(Guid.CreateVersion7()), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarSubFranja_DebeRetornar400_CuandoElIdDeRutaNoEsUnGuidValido()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new { franja = "22:00", tipo = "descanso", inicio = "02:00" };

        var response = await _client.PostAsJsonAsync(
            $"{RutaTurnos}/no-es-un-guid:quitar-subfranja", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
