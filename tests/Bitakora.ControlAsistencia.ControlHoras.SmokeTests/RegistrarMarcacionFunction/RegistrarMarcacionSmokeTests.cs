using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.RegistrarMarcacionFunction;

// HU-105: Smoke tests del endpoint POST control-horas/marcaciones
// Verifica camino feliz (202 + persistencia en Postgres), duplicado silencioso (202) y body malformado (400).
// CA-4: duplicado exacto retorna 202 silenciosamente, sin persistir ni publicar de nuevo.
// CA-6: tanto creacion exitosa como duplicado retornan 202 Accepted.
public class RegistrarMarcacionSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string Ruta = "/api/control-horas/marcaciones";
    private const string SchemaControlHoras = "control_horas";
    private const string TipoEvento = "marcacion_registrada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // CA-5: stream ID determinista = "{EmpleadoId}:{Timestamp:yyyy-MM-ddTHH:mm:ss}"
    // El timestamp que se usa para el stream ID es el crudo (antes de normalizar al minuto).
    private static string ComputarStreamId(string empleadoId, DateTime timestampCrudo) =>
        $"{empleadoId}:{timestampCrudo:yyyy-MM-ddTHH:mm:ss}";

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeRetornar202YPersistirEvento_CuandoMarcacionEsValida()
    {
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;

        // Arrange: timestamp fijo para que el stream ID sea determinista y el test sea reproducible.
        // CA-2: el handler trunca al minuto antes de emitir; el stream ID usa el timestamp crudo.
        var empleadoId = Guid.CreateVersion7().ToString();
        var timestamp = new DateTime(2026, 4, 17, 8, 9, 43, DateTimeKind.Utc);

        var payload = new
        {
            empleadoId,
            timestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ss") + "Z",
            tipoMarcacion = "ENTRADA",
            dispositivoId = "[TEST] DEV-SMOKE-001"
        };

        // Act
        var response = await _client.PostAsJsonAsync(Ruta, payload, ct);

        // Assert HTTP: 202 Accepted (CA-6)
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Assert persistencia: el evento marcacion_registrada debe existir en el stream
        var streamId = ComputarStreamId(empleadoId, timestamp);

        var existe = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEvento, Timeout);

        existe.Should().BeTrue(
            $"el evento {TipoEvento} deberia existir en el stream {streamId} tras registrar la marcacion");

        // Assert detallado: verificar contenido del evento persistido
        // CA-2: TimestampNormalizado = timestamp truncado al minuto (segundos = 0)
        var timestampNormalizado = new DateTime(2026, 4, 17, 8, 9, 0, DateTimeKind.Utc);

        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaControlHoras, streamId, TipoEvento,
            campoJson: "EmpleadoId", valorJson: empleadoId, TimeSpan.FromSeconds(5));

        eventoPersistido.GetProperty("EmpleadoId").GetString()
            .Should().Be(empleadoId);

        eventoPersistido.GetProperty("TipoMarcacion").GetString()
            .Should().Be("ENTRADA");

        eventoPersistido.GetProperty("DispositivoId").GetString()
            .Should().Be("[TEST] DEV-SMOKE-001");

        // CA-2: verificar que el timestamp fue truncado al minuto
        var timestampPersistido = eventoPersistido.GetProperty("TimestampNormalizado").GetDateTime();
        timestampPersistido.Should().Be(timestampNormalizado);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeRetornar202YPersistirEvento_CuandoMarcacionEsValidaSinCamposOpcionales()
    {
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;

        // Arrange: TipoMarcacion y DispositivoId son opcionales (CA-3), se envian como null
        var empleadoId = Guid.CreateVersion7().ToString();
        var timestamp = new DateTime(2026, 4, 17, 9, 30, 15, DateTimeKind.Utc);

        var payload = new
        {
            empleadoId,
            timestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ss") + "Z",
            tipoMarcacion = (string?)null,
            dispositivoId = (string?)null
        };

        // Act
        var response = await _client.PostAsJsonAsync(Ruta, payload, ct);

        // Assert HTTP
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Assert persistencia: el evento debe existir aunque los campos opcionales sean null
        var streamId = ComputarStreamId(empleadoId, timestamp);

        var existe = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEvento, Timeout);

        existe.Should().BeTrue(
            $"el evento {TipoEvento} deberia existir en el stream {streamId} incluso con campos opcionales nulos");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeRetornar202_CuandoMarcacionDuplicadaExacta()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange: mismo empleadoId y mismo timestamp -> mismo stream ID -> duplicado exacto (CA-4, CA-9)
        var empleadoId = Guid.CreateVersion7().ToString();
        var timestamp = new DateTime(2026, 4, 17, 10, 0, 0, DateTimeKind.Utc);

        var payload = new
        {
            empleadoId,
            timestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ss") + "Z",
            tipoMarcacion = "SALIDA",
            dispositivoId = "[TEST] DEV-SMOKE-DUP"
        };

        // Act 1: primera marcacion
        var primeraRespuesta = await _client.PostAsJsonAsync(Ruta, payload, ct);
        primeraRespuesta.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Act 2: duplicado exacto (mismo empleadoId + mismo timestamp = mismo stream ID)
        var segundaRespuesta = await _client.PostAsJsonAsync(Ruta, payload, ct);

        // CA-4, CA-6: duplicado silencioso -> 202 Accepted (no 409)
        segundaRespuesta.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeRetornar400_CuandoBodyEsMalformado()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange: JSON invalido (no parseable)
        var content = new StringContent("esto no es json", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(Ruta, content, ct);

        // Assert: JSON malformado -> 400 Bad Request
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeRetornar400_CuandoBodyEsNulo()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange: body completamente vacio
        var content = new StringContent("", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(Ruta, content, ct);

        // Assert: body nulo -> 400 Bad Request
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
