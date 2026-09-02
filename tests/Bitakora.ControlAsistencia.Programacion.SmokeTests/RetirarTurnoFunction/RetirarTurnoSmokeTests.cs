using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.RetirarTurnoFunction;

public class RetirarTurnoSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaTurnos = "/api/programacion/turnos";
    private const string SchemaProgramacion = "programacion";
    private const string TipoEventoTurnoRetirado = "turno_retirado";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // El nombre es unico en el catalogo (#497): sufijarlo con el turnoId mantiene estos smoke
    // tests re-ejecutables contra el mismo entorno dev.
    private static object PayloadTurnoConFranja(Guid turnoId, string nombreBase) => new
    {
        turnoId,
        nombre = $"{nombreBase} {turnoId}",
        ordinarias = new[]
        {
            new
            {
                inicio = "08:00:00",
                fin = "16:00:00",
                descansos = Array.Empty<object>(),
                extras = Array.Empty<object>()
            }
        }
    };

    private static object PayloadDescanso(Guid turnoId, string nombreBase) => new
    {
        turnoId,
        nombre = $"{nombreBase} {turnoId}",
        ordinarias = Array.Empty<object>(),
        esDescanso = true
    };

    private static string Ruta(Guid turnoId) => $"{RutaTurnos}/{turnoId}";

    private async Task CrearTurnoAsync(object payload, CancellationToken ct)
    {
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

    // CA-1: DELETE de un turno existente persiste TurnoRetirado en su stream. turno_retirado no
    // cruza ningun bus (issue #500): la persistencia en mt_events es el unico efecto secundario
    // verificable de este handler, mismo criterio que CrearTurnoSmokeTests para turno_creado.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarTurno_DebeRetornar202YPersistirTurnoRetirado_CuandoElTurnoExiste()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        await CrearTurnoAsync(PayloadTurnoConFranja(turnoId, "[TEST] Turno A Retirar"), ct);

        var response = await _client.DeleteAsync(Ruta(turnoId), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = turnoId.ToString();
        var existe = await postgres.ExisteEventoAsync(
            SchemaProgramacion, streamId, TipoEventoTurnoRetirado, Timeout);

        existe.Should().BeTrue(
            $"el evento {TipoEventoTurnoRetirado} deberia existir en el stream {streamId} tras retirar el turno");
    }

    // CA-5: retirar tambien aplica a turnos de descanso -- son turnos de pleno derecho del
    // catalogo (#423), sin discriminador propio en el aggregate mas alla de la lista vacia de
    // franjas.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarTurno_DebeRetornar202YPersistirTurnoRetirado_CuandoElTurnoEsDescanso()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        await CrearTurnoAsync(PayloadDescanso(turnoId, "[TEST] Descanso A Retirar"), ct);

        var response = await _client.DeleteAsync(Ruta(turnoId), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = turnoId.ToString();
        var existe = await postgres.ExisteEventoAsync(
            SchemaProgramacion, streamId, TipoEventoTurnoRetirado, Timeout);

        existe.Should().BeTrue(
            $"el evento {TipoEventoTurnoRetirado} deberia existir en el stream {streamId} tras retirar el descanso");
    }

    // CA-2: DELETE de un turno inexistente -> 404 con mensaje .resx.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarTurno_DebeRetornar404_CuandoElTurnoNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.DeleteAsync(Ruta(Guid.CreateVersion7()), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // CA-3: DELETE de un turno ya retirado -> 409 con mensaje .resx.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarTurno_DebeRetornar409_CuandoElTurnoYaEstaRetirado()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        await CrearTurnoAsync(PayloadTurnoConFranja(turnoId, "[TEST] Turno Doble Retiro"), ct);

        var primeraRespuesta = await _client.DeleteAsync(Ruta(turnoId), ct);
        primeraRespuesta.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que el primer retiro funcione");

        var response = await _client.DeleteAsync(Ruta(turnoId), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // El {id} de ruta se valida en el borde (MEF-ADR-0037 seccion 2): un id no-Guid nunca llega
    // al command router.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarTurno_DebeRetornar400_CuandoElIdDeRutaNoEsUnGuidValido()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.DeleteAsync($"{RutaTurnos}/no-es-un-guid", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
