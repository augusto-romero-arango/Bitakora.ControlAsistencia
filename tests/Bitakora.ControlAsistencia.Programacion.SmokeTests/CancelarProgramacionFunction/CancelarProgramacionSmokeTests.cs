using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.CancelarProgramacionFunction;

public class CancelarProgramacionSmokeTests(
    ApiFixture api, ServiceBusFixture serviceBus, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string TopicSalida = "cancelacion-turno-diario-solicitada";
    private const string Suscripcion = "smoke-tests";
    private const string SchemaProgramacion = "programacion";
    private const string TipoEventoCancelacionSolicitada = "cancelacion_programacion_solicitada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Forma minima del evento persistido: evita referenciar Programacion.DomainEvents desde los
    // smoke tests. La lectura case-insensitive la aporta EventoPersistido.OpcionesLectura.
    private sealed record ColaboradorMinimo(string Identificacion, string CodigoColaborador, string NombreCompleto);
    private sealed record CancelacionMinima(Guid Id, ColaboradorMinimo Colaborador, IReadOnlyList<DateOnly> Fechas);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CancelarProgramacion_PublicaUnEventoPorFechaYPersisteLaSolicitud_CuandoLaSolicitudEsAceptada()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;

        await serviceBus.PurgeAsync(TopicSalida, Suscripcion);

        var solicitudId = Guid.CreateVersion7();
        var identificacion = "CC-555666777";
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var nombreCompleto = "[TEST] Smoke Cancelacion Publicacion";
        var fecha1 = "2026-04-15";
        var fecha2 = "2026-04-16";
        var payload = new
        {
            id = solicitudId,
            colaborador = new { identificacion, codigoColaborador, nombreCompleto },
            fechas = new[] { fecha1, fecha2 }
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/cancelaciones", payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert: fan-out, un CancelacionTurnoDiarioSolicitada por fecha (CA-1)
        var evento1 = await serviceBus.WaitForMessageAsync<CancelacionTurnoDiarioSolicitada>(
            TopicSalida, Suscripcion, e => e.SolicitudId == solicitudId, Timeout);
        var evento2 = await serviceBus.WaitForMessageAsync<CancelacionTurnoDiarioSolicitada>(
            TopicSalida, Suscripcion, e => e.SolicitudId == solicitudId, Timeout);

        new[] { evento1.Fecha, evento2.Fecha }.Should()
            .BeEquivalentTo(new[] { DateOnly.Parse(fecha1), DateOnly.Parse(fecha2) });

        var colaboradorEsperado = new ResumenColaborador(identificacion, codigoColaborador, nombreCompleto);
        evento1.Colaborador.Should().Be(colaboradorEsperado);
        evento2.Colaborador.Should().Be(colaboradorEsperado);

        // Assert: la solicitud de cancelacion queda persistida en su propio stream (CA-1)
        var streamId = solicitudId.ToString();
        var json = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaProgramacion, streamId, TipoEventoCancelacionSolicitada,
            campoJson: "Id", valorJson: streamId, Timeout);

        var eventoPersistido = json.Deserialize<CancelacionMinima>(EventoPersistido.OpcionesLectura);
        eventoPersistido.Should().NotBeNull();
        eventoPersistido!.Colaborador.Should().Be(
            new ColaboradorMinimo(identificacion, codigoColaborador, nombreCompleto));
        eventoPersistido.Fechas.Should().BeEquivalentTo(
            new[] { DateOnly.Parse(fecha1), DateOnly.Parse(fecha2) });
    }

    private const string TipoEventoBorradorDescartado = "borrador_de_programacion_descartado";
    private sealed record DescarteMinimo(IReadOnlyList<DateOnly> Fechas);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CancelarProgramacion_DescartaLaFechaDelBorradorYEnviaLaCancelacion_CuandoLaFechaEstabaEnElBorrador()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        await serviceBus.PurgeAsync(TopicSalida, Suscripcion);

        using var tenant = new TenantDeTest(api);
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var identificacion = "CC-555666888";
        var nombreCompleto = "[TEST] Smoke Cancelacion Borrador";

        var turnoId = Guid.CreateVersion7();
        var turno = await tenant.Client.PostAsJsonAsync("/api/programacion/turnos", new
        {
            turnoId,
            nombre = $"[TEST] Turno Smoke Cancelacion Borrador {turnoId}",
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
        turno.StatusCode.Should().Be(HttpStatusCode.Created);

        var solicitud = await tenant.Client.PostAsJsonAsync("/api/programacion/solicitudes", new
        {
            id = Guid.CreateVersion7(),
            turnoId,
            colaborador = new { identificacion, codigoColaborador, nombreCompleto },
            fechas = new[] { "2026-12-15", "2026-12-16" }
        }, ct);
        solicitud.StatusCode.Should().Be(HttpStatusCode.Created);

        var cancelacionId = Guid.CreateVersion7();
        var response = await tenant.Client.PostAsJsonAsync("/api/programacion/cancelaciones", new
        {
            id = cancelacionId,
            colaborador = new { identificacion, codigoColaborador, nombreCompleto },
            fechas = new[] { "2026-12-15" }
        }, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var streamBorrador = $"bm:{codigoColaborador}:202612";
        var json = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaProgramacion, streamBorrador, TipoEventoBorradorDescartado,
            campoJson: "Fechas", valorJson: "[\"2026-12-15\"]", Timeout);
        json.Deserialize<DescarteMinimo>(EventoPersistido.OpcionesLectura)!.Fechas.Should()
            .BeEquivalentTo(new[] { new DateOnly(2026, 12, 15) });

        var evento = await serviceBus.WaitForMessageAsync<CancelacionTurnoDiarioSolicitada>(
            TopicSalida, Suscripcion, e => e.SolicitudId == cancelacionId, Timeout);
        evento.Fecha.Should().Be(new DateOnly(2026, 12, 15));
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CancelarProgramacion_DebeRetornar409_CuandoSolicitudYaExiste()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;
        await serviceBus.PurgeAsync(TopicSalida, Suscripcion);

        var solicitudId = Guid.CreateVersion7();
        var payload = new
        {
            id = solicitudId,
            colaborador = new
            {
                identificacion = "CC-111222333",
                codigoColaborador = Guid.CreateVersion7().ToString(),
                nombreCompleto = "[TEST] Smoke Cancelacion Duplicado"
            },
            fechas = new[] { "2026-04-20" }
        };

        var primeraRespuesta = await _client.PostAsJsonAsync("/api/programacion/cancelaciones", payload, ct);
        primeraRespuesta.StatusCode.Should().Be(HttpStatusCode.Created);

        await serviceBus.WaitForMessageAsync<CancelacionTurnoDiarioSolicitada>(
            TopicSalida, Suscripcion, e => e.SolicitudId == solicitudId, Timeout);

        var response = await _client.PostAsJsonAsync("/api/programacion/cancelaciones", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CancelarProgramacion_DebeRetornar400_CuandoIdEsGuidVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            id = Guid.Empty,
            colaborador = new
            {
                identificacion = "CC-123456789",
                codigoColaborador = Guid.CreateVersion7().ToString(),
                nombreCompleto = "[TEST] Juan Perez"
            },
            fechas = new[] { "2026-04-15" }
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/cancelaciones", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CancelarProgramacion_DebeRetornar400_CuandoColaboradorEsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            id = Guid.CreateVersion7(),
            colaborador = (object?)null,
            fechas = new[] { "2026-04-15" }
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/cancelaciones", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CancelarProgramacion_DebeRetornar400_CuandoColaboradorTieneCamposVacios()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            id = Guid.CreateVersion7(),
            colaborador = new
            {
                identificacion = "",
                codigoColaborador = "",
                nombreCompleto = ""
            },
            fechas = new[] { "2026-04-15" }
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/cancelaciones", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CancelarProgramacion_DebeRetornar400_CuandoFechasEstaVacia()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            id = Guid.CreateVersion7(),
            colaborador = new
            {
                identificacion = "CC-123456789",
                codigoColaborador = Guid.CreateVersion7().ToString(),
                nombreCompleto = "[TEST] Juan Perez"
            },
            fechas = Array.Empty<string>()
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/cancelaciones", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CancelarProgramacion_DebeRetornar400_CuandoFechasEstanDuplicadas()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            id = Guid.CreateVersion7(),
            colaborador = new
            {
                identificacion = "CC-123456789",
                codigoColaborador = Guid.CreateVersion7().ToString(),
                nombreCompleto = "[TEST] Juan Perez"
            },
            fechas = new[] { "2026-04-15", "2026-04-15" }
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/cancelaciones", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
