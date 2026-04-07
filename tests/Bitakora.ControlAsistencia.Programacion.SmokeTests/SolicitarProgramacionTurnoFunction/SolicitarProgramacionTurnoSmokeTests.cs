using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.SolicitarProgramacionTurnoFunction;

public class SolicitarProgramacionTurnoSmokeTests(ApiFixture api)
{
    private readonly HttpClient _client = api.Client;

    private static object PayloadValido(Guid? id = null, Guid? turnoId = null) => new
    {
        id = id ?? Guid.CreateVersion7(),
        turnoId = turnoId ?? Guid.CreateVersion7(),
        empleado = new
        {
            empleadoId = Guid.CreateVersion7().ToString(),
            tipoIdentificacion = "CC",
            numeroIdentificacion = "123456789",
            nombres = "[TEST] Juan Carlos",
            apellidos = "[TEST] Perez Lopez"
        },
        fechas = new[] { "2025-08-01", "2025-08-02" }
    };

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_DebeRetornar202_CuandoPayloadEsValido()
    {
        var ct = TestContext.Current.CancellationToken;

        // El turnoId debe existir en el catalogo; primero lo creamos
        var turnoId = Guid.CreateVersion7();
        var turnoPayload = new
        {
            turnoId,
            nombre = "[TEST] Turno para Programacion",
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
        var crearTurnoResponse = await _client.PostAsJsonAsync("/api/programacion/turnos", turnoPayload, ct);
        crearTurnoResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes", PayloadValido(turnoId: turnoId), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_DebeRetornar409_CuandoSolicitudYaExiste()
    {
        var ct = TestContext.Current.CancellationToken;

        // Crear turno en catalogo
        var turnoId = Guid.CreateVersion7();
        var turnoPayload = new
        {
            turnoId,
            nombre = "[TEST] Turno para Duplicado",
            ordinarias = new[]
            {
                new
                {
                    inicio = "07:00:00",
                    fin = "15:00:00",
                    descansos = Array.Empty<object>(),
                    extras = Array.Empty<object>()
                }
            }
        };
        await _client.PostAsJsonAsync("/api/programacion/turnos", turnoPayload, ct);

        var solicitudId = Guid.CreateVersion7();
        var payload = PayloadValido(id: solicitudId, turnoId: turnoId);

        await _client.PostAsJsonAsync("/api/programacion/solicitudes", payload, ct);
        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_DebeRetornar404_CuandoTurnoNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadValido(turnoId: Guid.CreateVersion7());

        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_DebeRetornar400_CuandoIdEsGuidVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            id = Guid.Empty,
            turnoId = Guid.CreateVersion7(),
            empleado = new
            {
                empleadoId = Guid.CreateVersion7().ToString(),
                tipoIdentificacion = "CC",
                numeroIdentificacion = "123456789",
                nombres = "[TEST] Juan",
                apellidos = "[TEST] Perez"
            },
            fechas = new[] { "2025-08-01" }
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_DebeRetornar400_CuandoTurnoIdEsGuidVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            id = Guid.CreateVersion7(),
            turnoId = Guid.Empty,
            empleado = new
            {
                empleadoId = Guid.CreateVersion7().ToString(),
                tipoIdentificacion = "CC",
                numeroIdentificacion = "123456789",
                nombres = "[TEST] Juan",
                apellidos = "[TEST] Perez"
            },
            fechas = new[] { "2025-08-01" }
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_DebeRetornar400_CuandoEmpleadoTieneCamposVacios()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            id = Guid.CreateVersion7(),
            turnoId = Guid.CreateVersion7(),
            empleado = new
            {
                empleadoId = "",
                tipoIdentificacion = "",
                numeroIdentificacion = "",
                nombres = "",
                apellidos = ""
            },
            fechas = new[] { "2025-08-01" }
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_DebeRetornar400_CuandoFechasEstaVacia()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            id = Guid.CreateVersion7(),
            turnoId = Guid.CreateVersion7(),
            empleado = new
            {
                empleadoId = Guid.CreateVersion7().ToString(),
                tipoIdentificacion = "CC",
                numeroIdentificacion = "123456789",
                nombres = "[TEST] Juan",
                apellidos = "[TEST] Perez"
            },
            fechas = Array.Empty<string>()
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
