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
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static string RutaAgregarFranja(Guid turnoId) => $"{RutaTurnos}/{turnoId}:agregar-franja";
    private static string RutaAgregarSubFranja(Guid turnoId) => $"{RutaTurnos}/{turnoId}:agregar-subfranja";

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

    // CA-7: segundo paso del diseno de turno por pasos -- crear el turno, agregarle la franja
    // nocturna, y agregarle un descanso de madrugada. descanso_agregado no cruza ningun bus:
    // mt_events es la unica ventana black-box a lo que quedo grabado.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AgregarSubFranja_DebeRetornar202YPersistirElDescansoDeMadrugada_CuandoLaFranjaEsNocturna()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        await CrearTurnoVacioAsync(turnoId, "[TEST] Turno Subfranja Nocturna", ct);
        await AgregarFranjaNocturnaAsync(turnoId, ct);

        var payload = new { franja = "22:00", tipo = "descanso", inicio = "02:00", fin = "02:30" };
        var response = await _client.PostAsJsonAsync(RutaAgregarSubFranja(turnoId), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = turnoId.ToString();
        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaProgramacion, streamId, TipoEventoDescansoAgregado,
            campoJson: "TurnoId", valorJson: turnoId.ToString(), Timeout);

        var descanso = eventoPersistido.GetProperty("Franja").GetProperty("descansos")[0];
        descanso.GetProperty("horaInicio").GetString().Should().Be("02:00:00");
        descanso.GetProperty("diaOffsetInicio").GetInt32().Should().Be(1);
        descanso.GetProperty("diaOffsetFin").GetInt32().Should().Be(1);
    }

    // CA-6: tipo desconocido -> 400 (validado por AgregarSubFranjaBodyValidator).
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

    // CA-3: turno inexistente -> 404.
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

    // CA-3: ninguna franja empieza a la hora especificada -> 409.
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
