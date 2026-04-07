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
}
