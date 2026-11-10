using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.AgregarSubFranjaFunction;

public class AgregarSubFranjaSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaTurnos = "/api/programacion/turnos";
    private const string SchemaProgramacion = "programacion";
    private const string TipoEventoDescansoAgregado = "descanso_agregado";
    private const string TipoEventoExtraAgregado = "extra_agregado";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static string RutaAgregarFranja(Guid turnoId) => $"{RutaTurnos}/{turnoId}:agregar-franja";
    private static string RutaAgregarSubFranja(Guid turnoId) => $"{RutaTurnos}/{turnoId}:agregar-subfranja";

    private async Task CrearTurnoVacioAsync(Guid turnoId, string nombreBase, CancellationToken ct)
    {
        var payload = new { turnoId, nombre = $"{nombreBase} {turnoId}" };
        var response = await _client.PostAsJsonAsync(RutaTurnos, payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que CrearTurno funcione");
    }

    private async Task AgregarFranjaNocturnaAsync(Guid turnoId, CancellationToken ct)
    {
        var payload = new { inicio = "22:00:00", fin = "06:00:00" };
        var response = await _client.PostAsJsonAsync(RutaAgregarFranja(turnoId), payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que AgregarFranja funcione");
    }

    // descanso_agregado no cruza ningun bus: mt_events es la unica ventana black-box a lo que
    // quedo grabado.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AgregarSubFranja_DebeRetornar204YPersistirElDescansoDeMadrugada_CuandoLaFranjaEsNocturna()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        await CrearTurnoVacioAsync(turnoId, "[TEST] Turno Subfranja Nocturna", ct);
        await AgregarFranjaNocturnaAsync(turnoId, ct);

        var payload = new { franja = "22:00", tipo = "descanso", inicio = "02:00", fin = "02:30" };
        var response = await _client.PostAsJsonAsync(RutaAgregarSubFranja(turnoId), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();

        var streamId = turnoId.ToString();
        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaProgramacion, streamId, TipoEventoDescansoAgregado,
            campoJson: "TurnoId", valorJson: turnoId.ToString(), Timeout);

        var descanso = eventoPersistido.GetProperty("Franja").GetProperty("descansos")[0];
        descanso.GetProperty("horaInicio").GetString().Should().Be("02:00:00");
        descanso.GetProperty("diaOffsetInicio").GetInt32().Should().Be(1);
        descanso.GetProperty("diaOffsetFin").GetInt32().Should().Be(1);
    }

    // El test de descanso ya cubre el camino feliz del comando; este solo cierra que "tipo: extra"
    // enruta al otro evento (extra_agregado).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AgregarSubFranja_DebeRetornar204YPersistirElExtraDeMadrugada_CuandoTipoEsExtra()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        await CrearTurnoVacioAsync(turnoId, "[TEST] Turno Subfranja Extra", ct);
        await AgregarFranjaNocturnaAsync(turnoId, ct);

        var payload = new { franja = "22:00", tipo = "extra", inicio = "05:00", fin = "06:00" };
        var response = await _client.PostAsJsonAsync(RutaAgregarSubFranja(turnoId), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();

        var streamId = turnoId.ToString();
        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaProgramacion, streamId, TipoEventoExtraAgregado,
            campoJson: "TurnoId", valorJson: turnoId.ToString(), Timeout);

        var extra = eventoPersistido.GetProperty("Franja").GetProperty("extras")[0];
        extra.GetProperty("horaInicio").GetString().Should().Be("05:00:00");
        extra.GetProperty("diaOffsetInicio").GetInt32().Should().Be(1);
        extra.GetProperty("diaOffsetFin").GetInt32().Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AgregarSubFranja_DebeRetornar400_CuandoElTipoEsDesconocido()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        await CrearTurnoVacioAsync(turnoId, "[TEST] Turno Subfranja Tipo Invalido", ct);
        await AgregarFranjaNocturnaAsync(turnoId, ct);

        var payload = new { franja = "22:00", tipo = "pausa", inicio = "02:00", fin = "02:30" };
        var response = await _client.PostAsJsonAsync(RutaAgregarSubFranja(turnoId), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AgregarSubFranja_DebeRetornar404_CuandoElTurnoNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new { franja = "22:00", tipo = "descanso", inicio = "02:00", fin = "02:30" };

        var response = await _client.PostAsJsonAsync(
            RutaAgregarSubFranja(Guid.CreateVersion7()), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AgregarSubFranja_DebeRetornar409_CuandoNingunaFranjaEmpiezaALaHoraEspecificada()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        await CrearTurnoVacioAsync(turnoId, "[TEST] Turno Subfranja Sin Franja", ct);
        await AgregarFranjaNocturnaAsync(turnoId, ct);

        var payload = new { franja = "23:00", tipo = "descanso", inicio = "02:00", fin = "02:30" };
        var response = await _client.PostAsJsonAsync(RutaAgregarSubFranja(turnoId), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
