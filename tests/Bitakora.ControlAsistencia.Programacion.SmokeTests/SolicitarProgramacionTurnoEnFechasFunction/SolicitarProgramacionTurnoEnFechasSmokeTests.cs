using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.SolicitarProgramacionTurnoEnFechasFunction;

public class SolicitarProgramacionTurnoEnFechasSmokeTests(ApiFixture api, ServiceBusFixture serviceBus) : IDisposable
{
    private readonly TenantDeTest _tenant = new(api);

    public void Dispose() => _tenant.Dispose();

    private HttpClient _client => _tenant.Client;

    private const string TopicSalida = "programacion-turno-en-fechas-solicitada";
    private const string Suscripcion = "smoke-tests";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurnoEnFechas_PublicaUnEventoConSesionYFechas_CuandoLaSolicitudEsAceptada()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

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
    }
}
