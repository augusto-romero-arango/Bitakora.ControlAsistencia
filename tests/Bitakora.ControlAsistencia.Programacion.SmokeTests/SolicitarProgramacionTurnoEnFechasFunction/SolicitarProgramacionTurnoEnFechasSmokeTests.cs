using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.SolicitarProgramacionTurnoEnFechasFunction;

public class SolicitarProgramacionTurnoEnFechasSmokeTests(ApiFixture api, ServiceBusFixture serviceBus, PostgresFixture postgres) : IDisposable
{
    private readonly TenantDeTest _tenant = new(api);

    public void Dispose() => _tenant.Dispose();

    private HttpClient _client => _tenant.Client;

    private const string TopicSalida = "programacion-turno-en-fechas-solicitada";
    private const string Suscripcion = "smoke-tests";
    private const string SchemaProgramacion = "programacion";
    private const string TipoEventoProgramacionSolicitada = "programacion_turno_solicitada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static object TurnoPayload(Guid turnoId) => new
    {
        turnoId,
        nombre = $"[TEST] Turno Smoke En Fechas {turnoId}",
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

    private static object Payload(Guid solicitudId, Guid turnoId, string[]? fechas = null) => new
    {
        id = solicitudId,
        turnoId,
        colaborador = new
        {
            identificacion = "CC-555666888",
            codigoColaborador = Guid.CreateVersion7().ToString(),
            nombreCompleto = "[TEST] Smoke En Fechas"
        },
        fechas = fechas ?? ["2026-04-15", "2026-04-16"]
    };

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurnoEnFechas_PublicaUnEventoConSesionYFechas_CuandoLaSolicitudEsAceptada()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        await serviceBus.PurgeAsync(TopicSalida, Suscripcion);

        var turnoId = Guid.CreateVersion7();
        var crearTurnoResponse = await _client.PostAsJsonAsync("/api/programacion/turnos", new
        {
            turnoId,
            nombre = $"[TEST] Turno Smoke En Fechas {turnoId}",
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
        }, ct);
        crearTurnoResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var solicitudId = Guid.CreateVersion7();
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes-en-fechas", new
        {
            id = solicitudId,
            turnoId,
            colaborador = new
            {
                identificacion = "CC-555666888",
                codigoColaborador,
                nombreCompleto = "[TEST] Smoke En Fechas"
            },
            fechas = new[] { "2026-04-15", "2026-04-16" }
        }, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var (evento, sessionId) = await serviceBus.WaitForMessageConSesionAsync<ProgramacionTurnoEnFechasSolicitada>(
            TopicSalida, Suscripcion, e => e.SolicitudId == solicitudId, Timeout);

        sessionId.Should().Be(codigoColaborador);
        evento.Fechas.Should().BeEquivalentTo(new[] { new DateOnly(2026, 4, 15), new DateOnly(2026, 4, 16) });

        var persistidos = await postgres.ContarEventosAsync(
            SchemaProgramacion, solicitudId.ToString(), TipoEventoProgramacionSolicitada);
        persistidos.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurnoEnFechas_Retorna409_CuandoLaSolicitudYaExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        (await _client.PostAsJsonAsync("/api/programacion/turnos", TurnoPayload(turnoId), ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = Payload(Guid.CreateVersion7(), turnoId);
        var primera = await _client.PostAsJsonAsync("/api/programacion/solicitudes-en-fechas", payload, ct);
        primera.StatusCode.Should().Be(HttpStatusCode.Created);

        var segunda = await _client.PostAsJsonAsync("/api/programacion/solicitudes-en-fechas", payload, ct);
        segunda.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurnoEnFechas_Retorna404_CuandoElTurnoNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes-en-fechas",
            Payload(Guid.CreateVersion7(), Guid.CreateVersion7()), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurnoEnFechas_Retorna400_CuandoFechasEstaVacia()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes-en-fechas",
            Payload(Guid.CreateVersion7(), Guid.CreateVersion7(), []), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
