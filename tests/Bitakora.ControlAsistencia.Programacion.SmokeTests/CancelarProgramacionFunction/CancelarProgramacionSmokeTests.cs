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

    // Forma minima del evento persistido: solo los campos que este test verifica, leidos de forma
    // case-insensitive (mismo criterio que SolicitarProgramacionTurnoSmokeTests).
    private sealed record ColaboradorMinimo(string Identificacion, string CodigoColaborador, string NombreCompleto);
    private sealed record CancelacionMinima(Guid Id, ColaboradorMinimo Colaborador, IReadOnlyList<DateOnly> Fechas);

    private static readonly JsonSerializerOptions OpcionesLectura = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

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

        var eventoPersistido = json.Deserialize<CancelacionMinima>(OpcionesLectura);
        eventoPersistido.Should().NotBeNull();
        eventoPersistido!.Colaborador.Should().Be(
            new ColaboradorMinimo(identificacion, codigoColaborador, nombreCompleto));
        eventoPersistido.Fechas.Should().BeEquivalentTo(
            new[] { DateOnly.Parse(fecha1), DateOnly.Parse(fecha2) });
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
        primeraRespuesta.StatusCode.Should().Be(HttpStatusCode.Accepted);

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
