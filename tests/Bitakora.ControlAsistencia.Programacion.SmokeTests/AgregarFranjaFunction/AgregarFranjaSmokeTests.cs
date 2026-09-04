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

    private async Task<Guid> CrearTurnoDescansoAsync(string nombreBase, CancellationToken ct)
    {
        var turnoId = Guid.CreateVersion7();
        var payload = new
        {
            turnoId,
            nombre = $"{nombreBase} {turnoId}",
            ordinarias = Array.Empty<object>(),
            esDescanso = true
        };
        var response = await _client.PostAsJsonAsync(RutaTurnos, payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que CrearTurno funcione");
        return turnoId;
    }

    private async Task RetirarTurnoAsync(Guid turnoId, CancellationToken ct)
    {
        var response = await _client.DeleteAsync($"{RutaTurnos}/{turnoId}", ct);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que RetirarTurno funcione");
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

    // CA-5: turno inexistente -> 404 (KeyNotFoundException, patron de RetirarTurnoFunction).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AgregarFranja_DebeRetornar404_CuandoElTurnoNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new { inicio = "08:00:00", fin = "16:00:00" };

        var response = await _client.PostAsJsonAsync(
            RutaAgregarFranja(Guid.CreateVersion7()), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // CA-4: precedencia retirado > descanso > solape -- un turno retirado rechaza la franja aunque
    // no tenga ninguna otra que se solape.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AgregarFranja_DebeRetornar409_CuandoElTurnoEstaRetirado()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        await CrearTurnoVacioAsync(turnoId, "[TEST] Turno Retirado Para Franja", ct);
        await RetirarTurnoAsync(turnoId, ct);

        var payload = new { inicio = "08:00:00", fin = "16:00:00" };
        var response = await _client.PostAsJsonAsync(RutaAgregarFranja(turnoId), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-4: un descanso rechaza la primera franja ordinaria.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AgregarFranja_DebeRetornar409_CuandoElTurnoEsDescanso()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = await CrearTurnoDescansoAsync("[TEST] Descanso Para Franja", ct);

        var payload = new { inicio = "08:00:00", fin = "16:00:00" };
        var response = await _client.PostAsJsonAsync(RutaAgregarFranja(turnoId), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // Invariante del VO (FranjaOrdinaria.Crear): Inicio == Fin sin offset explicito es una
    // duracion no positiva -- el handler deja subir la ArgumentException antes de tocar el
    // aggregate (CA-ADR-0030), el endpoint la traduce a 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AgregarFranja_DebeRetornar400_CuandoLaDuracionNoEsPositiva()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        await CrearTurnoVacioAsync(turnoId, "[TEST] Turno Duracion Invalida", ct);

        var payload = new { inicio = "10:00:00", fin = "10:00:00" };
        var response = await _client.PostAsJsonAsync(RutaAgregarFranja(turnoId), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // El {id} de ruta se valida en el borde (MEF-ADR-0037 seccion 2): un id no-Guid nunca llega
    // al command router.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AgregarFranja_DebeRetornar400_CuandoElIdDeRutaNoEsUnGuidValido()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new { inicio = "08:00:00", fin = "16:00:00" };

        var response = await _client.PostAsJsonAsync(
            $"{RutaTurnos}/no-es-un-guid:agregar-franja", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
