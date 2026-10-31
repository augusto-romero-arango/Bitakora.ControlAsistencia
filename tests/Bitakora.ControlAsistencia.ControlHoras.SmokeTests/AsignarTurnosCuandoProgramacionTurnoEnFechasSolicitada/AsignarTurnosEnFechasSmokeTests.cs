using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Microsoft.Extensions.Configuration;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.AsignarTurnosCuandoProgramacionTurnoEnFechasSolicitada;

public class AsignarTurnosEnFechasSmokeTests(ServiceBusFixture serviceBus, PostgresFixture postgres)
{
    private const string RutaSolicitud = "/api/programacion/solicitudes-en-fechas";
    private const string TopicDiasDepurados = "dias-depurados";
    private const string SuscripcionSmokeTests = "smoke-tests";
    private const string SchemaControlHoras = "control_horas";
    private const string TipoTurnoDiarioAsignado = "turno_diario_asignado";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    private static readonly DateOnly[] Fechas = [new(2026, 4, 13), new(2026, 4, 14)];

    private static HttpClient CrearClienteProgramacion()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var baseUrl = configuration["Programacion:BaseUrl"]
            ?? throw new InvalidOperationException(
                "Programacion:BaseUrl no esta configurado. Usa appsettings.json, appsettings.local.json o la variable Programacion__BaseUrl.");

        var identidad = IdentidadDePrueba.Desde(configuration);
        var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        client.DefaultRequestHeaders.Add("X-Tenant-Id", identidad.TenantId);
        client.DefaultRequestHeaders.Add("X-User-Id", identidad.UserId);
        client.DefaultRequestHeaders.Add("X-Organization-Membership-Id", identidad.OrganizationMembershipId);
        return client;
    }

    private static async Task<Guid> CrearTurnoAsync(HttpClient programacion, CancellationToken ct)
    {
        var turnoId = Guid.CreateVersion7();
        var response = await programacion.PostAsJsonAsync("/api/programacion/turnos", new
        {
            turnoId,
            nombre = $"[TEST] Turno Smoke Lote {turnoId}",
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

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ProgramacionTurnoEnFechasSolicitada_PublicaDiasDepuradosConLasFotosDelLote_CuandoSeSolicitanDosFechas()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        using var programacion = CrearClienteProgramacion();
        await serviceBus.PurgeAsync(TopicDiasDepurados, SuscripcionSmokeTests);
        var turnoId = await CrearTurnoAsync(programacion, ct);
        var solicitudId = Guid.CreateVersion7();
        var codigoColaborador = Guid.CreateVersion7().ToString();

        var solicitud = await programacion.PostAsJsonAsync(RutaSolicitud, new
        {
            id = solicitudId,
            turnoId,
            colaborador = new
            {
                identificacion = "CC-555666999",
                codigoColaborador,
                nombreCompleto = "[TEST] Smoke Lote En Fechas"
            },
            fechas = Fechas.Select(f => f.ToString("yyyy-MM-dd")).ToArray()
        }, ct);

        solicitud.StatusCode.Should().Be(HttpStatusCode.Created);

        var (diasDepurados, sessionId) = await serviceBus.WaitForMessageConSesionAsync<DiasDepurados>(
            TopicDiasDepurados, SuscripcionSmokeTests, e => e.CodigoColaborador == codigoColaborador, Timeout);

        sessionId.Should().Be(codigoColaborador);
        diasDepurados.Dias.Select(d => d.Fecha).Should().Equal(Fechas);
        diasDepurados.Dias.Should().OnlyContain(d => d.NombreTurno == $"[TEST] Turno Smoke Lote {turnoId}");

        foreach (var fecha in Fechas)
        {
            var streamId = $"cd:{codigoColaborador}:{fecha:yyyyMMdd}";
            var persistido = await postgres.ExisteEventoAsync(
                SchemaControlHoras, streamId, TipoTurnoDiarioAsignado, Timeout,
                campoJson: "SolicitudId", valorJson: solicitudId.ToString());
            persistido.Should().BeTrue(
                $"el lote deberia persistir {TipoTurnoDiarioAsignado} con SolicitudId {solicitudId} en {streamId}");
        }
    }
}
