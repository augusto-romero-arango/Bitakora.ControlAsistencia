using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.CrearTurnoFunction;

public class CrearTurnoSmokeTests(ApiFixture api)
{
    private readonly HttpClient _client = api.Client;

    private static object PayloadValido(Guid? turnoId = null, string nombre = "[TEST] Turno Diurno") => new
    {
        turnoId = turnoId ?? Guid.CreateVersion7(),
        nombre,
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
    public async Task CrearTurno_DebeRetornar202_CuandoPayloadEsValido()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.PostAsJsonAsync("/api/programacion/turnos", PayloadValido(), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurno_DebeRetornar409_CuandoTurnoYaExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        var payload = PayloadValido(turnoId);

        await _client.PostAsJsonAsync("/api/programacion/turnos", payload, ct);
        var response = await _client.PostAsJsonAsync("/api/programacion/turnos", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurno_DebeRetornar400_CuandoPayloadEsInvalido()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            turnoId = Guid.Empty,
            nombre = "",
            ordinarias = Array.Empty<object>()
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/turnos", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Issue #335 CA-1: turno "partido" con sede prearmada por franja (ej. narrativa del issue:
    // "Vigilante partido" -> manana en Suba, tarde en Chapinero). Solo se verifica el status code:
    // TurnoCreado no cruza el bus (no hay Service Bus que consumir) y este dominio no expone un
    // endpoint GET del catalogo ni tiene PostgresFixture -- no hay forma black-box de inspeccionar
    // el contenido persistido desde este smoke test.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurno_DebeRetornar202_CuandoAlgunasFranjasTienenSedePrearmada()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            turnoId = Guid.CreateVersion7(),
            nombre = "[TEST] Vigilante Partido",
            ordinarias = new object[]
            {
                new
                {
                    inicio = "06:00:00",
                    fin = "12:00:00",
                    descansos = Array.Empty<object>(),
                    extras = Array.Empty<object>(),
                    sede = new { id = "SEDE-SUBA", nombre = "[TEST] Suba" }
                },
                new
                {
                    inicio = "13:00:00",
                    fin = "19:00:00",
                    descansos = Array.Empty<object>(),
                    extras = Array.Empty<object>(),
                    sede = new { id = "SEDE-CHAPINERO", nombre = "[TEST] Chapinero" }
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/turnos", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    // Issue #335 CA-2: regresion -- una franja sin la clave "sede" en el payload conserva el
    // comportamiento actual (mismo camino que CrearTurno_DebeRetornar202_CuandoPayloadEsValido,
    // que ya usa PayloadValido() sin sede).

    // Issue #335 CA-3: sede presente pero con Id vacio se rechaza junto a las demas invariantes
    // de la franja (error acumulado en TurnoCreado.Crear -> AggregateException -> 400).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurno_DebeRetornar400_CuandoSedeDeFranjaTieneIdVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            turnoId = Guid.CreateVersion7(),
            nombre = "[TEST] Turno Sede Incompleta",
            ordinarias = new object[]
            {
                new
                {
                    inicio = "08:00:00",
                    fin = "16:00:00",
                    descansos = Array.Empty<object>(),
                    extras = Array.Empty<object>(),
                    sede = new { id = "", nombre = "[TEST] Suba" }
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/turnos", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Issue #335 CA-3: sede presente pero con Nombre en blanco se rechaza igual que Id vacio.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurno_DebeRetornar400_CuandoSedeDeFranjaTieneNombreEnBlanco()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            turnoId = Guid.CreateVersion7(),
            nombre = "[TEST] Turno Sede Incompleta",
            ordinarias = new object[]
            {
                new
                {
                    inicio = "08:00:00",
                    fin = "16:00:00",
                    descansos = Array.Empty<object>(),
                    extras = Array.Empty<object>(),
                    sede = new { id = "SEDE-SUBA", nombre = "   " }
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/turnos", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
