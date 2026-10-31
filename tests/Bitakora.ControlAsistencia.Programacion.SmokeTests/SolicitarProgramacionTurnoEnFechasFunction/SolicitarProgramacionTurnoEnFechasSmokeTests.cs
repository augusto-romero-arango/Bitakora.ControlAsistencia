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

    private const string Ruta = "/api/programacion/solicitudes-en-fechas";
    private const string TopicSalida = "programacion-turno-en-fechas-solicitada";
    private const string Suscripcion = "smoke-tests";
    private const string SchemaProgramacion = "programacion";
    private const string TipoEventoProgramacionSolicitada = "programacion_turno_solicitada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private static readonly string[] FechasPedidas = ["2026-04-15", "2026-04-16"];

    private async Task<Guid> CrearTurnoAsync(CancellationToken ct)
    {
        var turnoId = Guid.CreateVersion7();
        var response = await _client.PostAsJsonAsync("/api/programacion/turnos", new
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
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return turnoId;
    }

    private static object Payload(Guid solicitudId, Guid turnoId, string codigoColaborador, string[]? fechas = null) => new
    {
        id = solicitudId,
        turnoId,
        colaborador = new
        {
            identificacion = "CC-555666888",
            codigoColaborador,
            nombreCompleto = "[TEST] Smoke En Fechas"
        },
        fechas = fechas ?? FechasPedidas
    };

    private static string CodigoColaboradorUnico() => Guid.CreateVersion7().ToString();

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurnoEnFechas_PublicaUnEventoConSesionYFechas_CuandoLaSolicitudEsAceptada()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        await serviceBus.PurgeAsync(TopicSalida, Suscripcion);
        var turnoId = await CrearTurnoAsync(ct);
        var solicitudId = Guid.CreateVersion7();
        var codigoColaborador = CodigoColaboradorUnico();

        var response = await _client.PostAsJsonAsync(Ruta, Payload(solicitudId, turnoId, codigoColaborador), ct);

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
        var turnoId = await CrearTurnoAsync(ct);
        var payload = Payload(Guid.CreateVersion7(), turnoId, CodigoColaboradorUnico());

        var primera = await _client.PostAsJsonAsync(Ruta, payload, ct);
        primera.StatusCode.Should().Be(HttpStatusCode.Created);

        var segunda = await _client.PostAsJsonAsync(Ruta, payload, ct);
        segunda.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurnoEnFechas_Retorna404_CuandoElTurnoNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync(Ruta,
            Payload(Guid.CreateVersion7(), Guid.CreateVersion7(), CodigoColaboradorUnico()), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurnoEnFechas_Retorna400_CuandoFechasEstaVacia()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync(Ruta,
            Payload(Guid.CreateVersion7(), Guid.CreateVersion7(), CodigoColaboradorUnico(), []), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
