using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.QuitarTurnoDeDiaDePlantillaSemanalFunction;

// El evento no cruza ningun bus: mt_events es la unica ventana black-box al efecto secundario del
// handler (mismo criterio que AsignarTurnoADiaDePlantillaSemanalSmokeTests).
public class QuitarTurnoDeDiaDePlantillaSemanalSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private const string SchemaProgramacion = "programacion";
    private const string TipoEventoDiaQuitado = "dia_de_plantilla_semanal_quitado";
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
    private async Task<Guid> CrearPlantillaConDiaAsignadoAsync(CancellationToken ct)
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

        var asignarResponse = await _client.PutAsJsonAsync(
            $"/api/programacion/plantillas-semanales/{plantillaId}/dias/1/5", new { turnoId }, ct);
        asignarResponse.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que AsignarTurnoADiaDePlantillaSemanal funcione");

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
    public async Task QuitarTurnoDeDia_DebeRetornar204YPersistirElRetiroDelDia_CuandoElDiaTieneTurno()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var plantillaId = await CrearPlantillaConDiaAsignadoAsync(ct);

        var response = await _client.DeleteAsync(
            $"/api/programacion/plantillas-semanales/{plantillaId}/dias/1/5", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var streamId = plantillaId.ToString();
        var existe = await postgres.ExisteEventoAsync(
            SchemaProgramacion, streamId, TipoEventoDiaQuitado, Timeout);

        existe.Should().BeTrue(
            $"el evento {TipoEventoDiaQuitado} deberia existir en el stream {streamId} tras quitar el dia");
    }

    // DELETE es idempotente (RFC 9110 seccion 9.2.2): repetirlo sobre un dia ya vacio responde
    // 204 igual, sin re-emitir.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarTurnoDeDia_DebeRetornar204_CuandoSeRepiteSobreUnDiaYaVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var plantillaId = await CrearPlantillaConDiaAsignadoAsync(ct);
        var ruta = $"/api/programacion/plantillas-semanales/{plantillaId}/dias/1/5";

        var primeraRespuesta = await _client.DeleteAsync(ruta, ct);
        primeraRespuesta.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que el primer retiro funcione");

        var response = await _client.DeleteAsync(ruta, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarTurnoDeDia_DebeRetornar409_CuandoLaSemanaSuperaElTotalDeLaPlantilla()
    {
        var ct = TestContext.Current.CancellationToken;
        var plantillaId = await CrearPlantillaConDiaAsignadoAsync(ct);

        var response = await _client.DeleteAsync(
            $"/api/programacion/plantillas-semanales/{plantillaId}/dias/3/5", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarTurnoDeDia_DebeRetornar400_CuandoElDiaEstaFueraDeLaSemanaIso()
    {
        var ct = TestContext.Current.CancellationToken;
        var plantillaId = await CrearPlantillaConDiaAsignadoAsync(ct);

        var response = await _client.DeleteAsync(
            $"/api/programacion/plantillas-semanales/{plantillaId}/dias/1/8", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarTurnoDeDia_DebeRetornar404_CuandoLaPlantillaNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.DeleteAsync(
            $"/api/programacion/plantillas-semanales/{Guid.CreateVersion7()}/dias/1/5", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
