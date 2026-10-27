using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.RetirarPlantillaTurnosFunction;

// El evento no cruza ningun bus: mt_events es la unica ventana black-box al efecto secundario del
// handler (mismo criterio que QuitarTurnoDeDiaDePlantillaSemanalSmokeTests).
public class RetirarPlantillaTurnosSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private const string SchemaProgramacion = "programacion";
    private const string TipoEventoPlantillaRetirada = "plantilla_de_turnos_retirada";
    private const int DiasDelCiclo = 8;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _client = api.Client;

    private static object PayloadPlantillaValida(Guid plantillaId) => new
    {
        plantillaId,
        nombre = $"[TEST] Plantilla {plantillaId}",
        diasDelCiclo = DiasDelCiclo
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

    private async Task<Guid> CrearPlantillaAsync(CancellationToken ct)
    {
        var plantillaId = Guid.CreateVersion7();

        var response = await _client.PostAsJsonAsync(
            "/api/programacion/plantillas-turnos", PayloadPlantillaValida(plantillaId), ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que CrearPlantillaTurnos funcione");

        return plantillaId;
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
    public async Task RetirarPlantillaTurnos_DebeRetornar204YPersistirPlantillaDeTurnosRetirada_CuandoLaPlantillaExiste()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var plantillaId = await CrearPlantillaAsync(ct);

        var response = await _client.DeleteAsync($"/api/programacion/plantillas-turnos/{plantillaId}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var streamId = plantillaId.ToString();
        var existe = await postgres.ExisteEventoAsync(
            SchemaProgramacion, streamId, TipoEventoPlantillaRetirada, Timeout);
        existe.Should().BeTrue(
            $"el evento {TipoEventoPlantillaRetirada} deberia existir en el stream {streamId}");
    }

    // DELETE es idempotente (RFC 9110 seccion 9.2.2): el segundo retiro no re-emite ni conflictua.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarPlantillaTurnos_DebeRetornar204SinNuevosEventos_CuandoSeRepiteSobreUnaPlantillaYaRetirada()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var plantillaId = await CrearPlantillaAsync(ct);
        var ruta = $"/api/programacion/plantillas-turnos/{plantillaId}";

        var primeraRespuesta = await _client.DeleteAsync(ruta, ct);
        primeraRespuesta.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que el primer retiro funcione");

        var streamId = plantillaId.ToString();
        (await postgres.ExisteEventoAsync(
            SchemaProgramacion, streamId, TipoEventoPlantillaRetirada, Timeout))
            .Should().BeTrue("el primer retiro debe quedar persistido antes de la linea base");
        var eventosAntes = await postgres.ContarEventosDeStreamAsync(
            SchemaProgramacion, postgres.TenantId, streamId);

        var response = await _client.DeleteAsync(ruta, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var eventosDespues = await postgres.ContarEventosDeStreamAsync(
            SchemaProgramacion, postgres.TenantId, streamId);
        eventosDespues.Should().Be(eventosAntes);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarPlantillaTurnos_DebeRetornar409_CuandoSeIntentaAsignarUnDiaTrasElRetiro()
    {
        var ct = TestContext.Current.CancellationToken;
        var plantillaId = await CrearPlantillaAsync(ct);
        var turnoId = Guid.CreateVersion7();

        var turnoResponse = await _client.PostAsJsonAsync(
            "/api/programacion/turnos", PayloadTurnoValido(turnoId), ct);
        turnoResponse.IsSuccessStatusCode.Should().BeTrue(
            "el arrange de este smoke test depende de que CrearTurno acepte el comando");

        var retiroResponse = await _client.DeleteAsync(
            $"/api/programacion/plantillas-turnos/{plantillaId}", ct);
        retiroResponse.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que el retiro de la plantilla funcione");

        var response = await _client.PutAsJsonAsync(
            $"/api/programacion/plantillas-turnos/{plantillaId}/dias/1", new { turnoId }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarPlantillaTurnos_DebeRetornar404_CuandoLaPlantillaNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.DeleteAsync(
            $"/api/programacion/plantillas-turnos/{Guid.CreateVersion7()}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
