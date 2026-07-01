using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.Eventos;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.RegistrarMarcacionFunction;

// HU-105: Smoke tests del endpoint POST control-horas/marcaciones
// Verifica camino feliz (202 + persistencia en Postgres), duplicado silencioso (202) y body malformado (400).
// CA-4: duplicado exacto retorna 202 silenciosamente, sin persistir ni publicar de nuevo.
// CA-6: tanto creacion exitosa como duplicado retornan 202 Accepted.
// HU-108: cobertura adicional de los efectos del handler in-process AdicionarMarcacionCuandoMarcacionRegistrada,
// que tras el POST persiste marcacion_adicionada y publica DiaCalculado al topic dia-calculado.
public class RegistrarMarcacionSmokeTests(
    ApiFixture api,
    PostgresFixture postgres,
    ServiceBusFixture serviceBus)
{
    private readonly HttpClient _client = api.Client;

    private const string Ruta = "/api/control-horas/marcaciones";
    private const string SchemaControlHoras = "control_horas";
    private const string TipoEvento = "marcacion_registrada";
    private const string TipoEventoMarcacionAdicionada = "marcacion_adicionada";
    private const string TipoEventoTurnoDiarioAsignado = "turno_diario_asignado";
    private const string TopicProgramacionEntrada = "programacion-turno-diario-solicitada";
    private const string TopicDiaCalculado = "dia-calculado";
    private const string SuscripcionSmokeTests = "smoke-tests";
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

    // HU-108: tras el POST, el handler in-process AdicionarMarcacionCuandoMarcacionRegistrada
    // persiste marcacion_adicionada en el stream {empleadoId}:{fecha} y publica DiaCalculado.
    // Setup: se publica programacion-turno-diario-solicitada para que el aggregate tenga
    // turno previo, asegurando que DiaCalculado.InformacionEmpleado no sea null y se pueda
    // filtrar por EmpleadoId en la suscripcion smoke-tests.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebePublicarDiaCalculadoYPersistirMarcacionAdicionada_CuandoMarcacionGeneraNuevoEvento()
    {
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        // Arrange: identificadores unicos por ejecucion
        var empleadoId = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 4, 27);
        var streamId = $"{empleadoId}:{fecha:yyyy-MM-dd}";

        // Setup: publicar programacion-turno-diario-solicitada y esperar a que ControlHoras
        // persista turno_diario_asignado. Asi el ControlDiario tendra TurnoDiarioAsignado previo
        // antes de procesar la marcacion.
        var solicitudId = Guid.CreateVersion7();
        var programacionPayload = new
        {
            SolicitudId = solicitudId,
            Empleado = new
            {
                EmpleadoId = empleadoId,
                TipoIdentificacion = "CC",
                NumeroIdentificacion = "888777666",
                Nombres = "[TEST] Smoke DiaCalculado",
                Apellidos = "[TEST] HU108"
            },
            Fecha = fecha.ToString("yyyy-MM-dd"),
            DetalleTurno = new
            {
                Nombre = "[TEST] Turno HU108",
                FranjasOrdinarias = new[]
                {
                    new
                    {
                        HoraInicio = "08:00:00",
                        HoraFin = "16:00:00",
                        DiaOffsetFin = 0,
                        Descansos = Array.Empty<object>(),
                        Extras = Array.Empty<object>()
                    }
                }
            }
        };

        await serviceBus.PublishAsync(TopicProgramacionEntrada, programacionPayload, solicitudId.ToString());

        var turnoAsignado = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoTurnoDiarioAsignado, Timeout,
            campoJson: "SolicitudId", valorJson: solicitudId.ToString());

        turnoAsignado.Should().BeTrue(
            $"el evento {TipoEventoTurnoDiarioAsignado} deberia existir antes de registrar la marcacion");

        // Arrange: purgar la suscripcion smoke-tests del topic dia-calculado para que cualquier
        // mensaje recibido tras el Act sea de este test (patron purge-before-act, ADR-0016).
        await serviceBus.PurgeAsync(TopicDiaCalculado, SuscripcionSmokeTests);

        // Arrange: marcacion dentro de la franja programada (entrada 08:00-16:00).
        // Timestamp fuera de ventana nocturna -> el handler procesa una sola fecha.
        var timestamp = new DateTime(fecha, new TimeOnly(8, 0, 0), DateTimeKind.Utc);
        var payload = new
        {
            empleadoId,
            timestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ss") + "Z",
            tipoMarcacion = "ENTRADA",
            dispositivoId = "[TEST] DEV-SMOKE-HU108"
        };

        // Act
        var response = await _client.PostAsJsonAsync(Ruta, payload, ct);

        // Assert HTTP: 202 Accepted
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Assert persistencia: marcacion_adicionada en el stream del ControlDiario.
        var marcacionAdicionada = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoMarcacionAdicionada, Timeout,
            campoJson: "EmpleadoId", valorJson: empleadoId);

        marcacionAdicionada.Should().BeTrue(
            $"el evento {TipoEventoMarcacionAdicionada} deberia existir en el stream {streamId} tras el POST");

        // Assert publicacion: DiaCalculado emitido al topic dia-calculado, filtrado por EmpleadoId.
        var diaCalculado = await serviceBus.WaitForMessageAsync<DiaCalculado>(
            TopicDiaCalculado, SuscripcionSmokeTests,
            e => e.InformacionEmpleado != null && e.InformacionEmpleado.EmpleadoId == empleadoId,
            Timeout);

        diaCalculado.Fecha.Should().Be(fecha);
        diaCalculado.InformacionEmpleado!.EmpleadoId.Should().Be(empleadoId);
        // Issue #183 CA-6: el payload viaja plano (HorasDiscriminadas) y se deserializo con el
        // serializador POR DEFECTO del fixture (sin resolver custom). Esta marcacion es solo ENTRADA:
        // la franja queda anomala (sin salida) -> sin minutos calculables -> MinutosPorConcepto vacio.
        diaCalculado.HorasDiscriminadas.Should().NotBeNull(
            "DiaCalculado siempre se emite con HorasDiscriminadas");
        diaCalculado.HorasDiscriminadas.MinutosPorConcepto.Should().BeEmpty(
            "una entrada sola deja la franja anomala, sin minutos por concepto");

        // Assert: ausencia de dead letters en la suscripcion smoke-tests del topic dia-calculado.
        var deadLetters = await serviceBus.PeekDeadLetterMessagesAsync(
            TopicDiaCalculado, SuscripcionSmokeTests);

        deadLetters.Should().BeEmpty(
            "no deberia haber mensajes en dead letter de '{0}' del topic '{1}'",
            SuscripcionSmokeTests, TopicDiaCalculado);
    }

    // HU-181 CA-5: camino feliz completo (turno + entrada + salida) -> el DiaCalculado publicado
    // tras la salida lleva el DesgloseHoras REAL consolidado del dia, no DesgloseHoras.Vacio.
    // Cada marcacion publica su propio DiaCalculado: la entrada deja la franja anomala (sin salida)
    // y la salida la completa. Para capturar el evento posterior a la salida se purga la suscripcion
    // smoke-tests DESPUES de confirmar que la entrada quedo persistida (su DiaCalculado ya se publico)
    // y ANTES de registrar la salida (patron purge-before-act, ADR-0016).
    // Asercion minima: el desglose real fluye end-to-end (OrdinariaDiurna > 0, FranjasAnomalas == 0);
    // la matematica detallada del calculo esta cubierta en unit (#116/#136/#139).
    // No modifica el smoke de franja incompleta (entrada-only) de arriba: ambos coexisten.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarMarcacion_PublicaDiaCalculadoConDesgloseReal_CuandoMarcacionesCompletanLaFranja()
    {
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        // Arrange: identificadores unicos por ejecucion.
        var empleadoId = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 4, 28);
        var streamId = $"{empleadoId}:{fecha:yyyy-MM-dd}";

        // Setup: turno 08:00-16:00 via Service Bus; esperar a que ControlHoras persista
        // turno_diario_asignado antes de registrar las marcaciones.
        var solicitudId = Guid.CreateVersion7();
        var programacionPayload = new
        {
            SolicitudId = solicitudId,
            Empleado = new
            {
                EmpleadoId = empleadoId,
                TipoIdentificacion = "CC",
                NumeroIdentificacion = "777666555",
                Nombres = "[TEST] Smoke Desglose Real",
                Apellidos = "[TEST] HU181"
            },
            Fecha = fecha.ToString("yyyy-MM-dd"),
            DetalleTurno = new
            {
                Nombre = "[TEST] Turno HU181",
                FranjasOrdinarias = new[]
                {
                    new
                    {
                        HoraInicio = "08:00:00",
                        HoraFin = "16:00:00",
                        DiaOffsetFin = 0,
                        Descansos = Array.Empty<object>(),
                        Extras = Array.Empty<object>()
                    }
                }
            }
        };

        await serviceBus.PublishAsync(TopicProgramacionEntrada, programacionPayload, solicitudId.ToString());

        var turnoAsignado = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoTurnoDiarioAsignado, Timeout,
            campoJson: "SolicitudId", valorJson: solicitudId.ToString());

        turnoAsignado.Should().BeTrue(
            $"el evento {TipoEventoTurnoDiarioAsignado} deberia existir antes de registrar las marcaciones");

        // Act 1: ENTRADA 08:00. La franja queda con entrada pero sin salida (aun anomala).
        var entradaTimestamp = new DateTime(fecha, new TimeOnly(8, 0, 0), DateTimeKind.Utc);
        var entradaResponse = await _client.PostAsJsonAsync(Ruta, new
        {
            empleadoId,
            timestamp = entradaTimestamp.ToString("yyyy-MM-ddTHH:mm:ss") + "Z",
            tipoMarcacion = "ENTRADA",
            dispositivoId = "[TEST] DEV-SMOKE-HU181"
        }, ct);
        entradaResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Esperar a que la entrada quede persistida: garantiza que su DiaCalculado ya fue publicado
        // antes de purgar, de modo que el purge elimine el evento de la entrada (no el de la salida).
        var entradaPersistida = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoMarcacionAdicionada, Timeout,
            campoJson: "EmpleadoId", valorJson: empleadoId);

        entradaPersistida.Should().BeTrue(
            $"el evento {TipoEventoMarcacionAdicionada} de la entrada deberia existir antes de purgar");

        // Purge-before-act: limpiar la suscripcion para que el unico DiaCalculado restante de este
        // empleado sea el que publique la salida (descarta los del turno y la entrada).
        await serviceBus.PurgeAsync(TopicDiaCalculado, SuscripcionSmokeTests);

        // Act 2: SALIDA 16:00. Completa la franja (entrada 08:00 + salida 16:00) -> desglose real.
        var salidaTimestamp = new DateTime(fecha, new TimeOnly(16, 0, 0), DateTimeKind.Utc);
        var salidaResponse = await _client.PostAsJsonAsync(Ruta, new
        {
            empleadoId,
            timestamp = salidaTimestamp.ToString("yyyy-MM-ddTHH:mm:ss") + "Z",
            tipoMarcacion = "SALIDA",
            dispositivoId = "[TEST] DEV-SMOKE-HU181"
        }, ct);
        salidaResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Assert publicacion: el DiaCalculado posterior a la salida lleva el desglose real,
        // filtrado por EmpleadoId (unico por ejecucion).
        var diaCalculado = await serviceBus.WaitForMessageAsync<DiaCalculado>(
            TopicDiaCalculado, SuscripcionSmokeTests,
            e => e.InformacionEmpleado != null && e.InformacionEmpleado.EmpleadoId == empleadoId,
            Timeout);

        diaCalculado.Fecha.Should().Be(fecha);

        // Issue #183 CA-6: el smoke verifica MinutosPorConcepto del payload plano, deserializado con el
        // serializador POR DEFECTO del fixture (sin resolver custom). La franja quedo completa (entrada
        // 08:00 + salida 16:00), asi que una jornada 08:00-16:00 acumula horas ordinarias diurnas reales.
        // Clave = Concepto.ToString() ("OrdinariaDiurna").
        diaCalculado.HorasDiscriminadas.MinutosPorConcepto
            .Should().ContainKey("OrdinariaDiurna");
        diaCalculado.HorasDiscriminadas.MinutosPorConcepto["OrdinariaDiurna"]
            .Should().BeGreaterThan(0,
                "el desglose real discriminado lleva horas ordinarias diurnas (no un payload vacio)");

        // Assert: ausencia de dead letters en la suscripcion smoke-tests del topic dia-calculado.
        var deadLetters = await serviceBus.PeekDeadLetterMessagesAsync(
            TopicDiaCalculado, SuscripcionSmokeTests);

        deadLetters.Should().BeEmpty(
            "no deberia haber mensajes en dead letter de '{0}' del topic '{1}'",
            SuscripcionSmokeTests, TopicDiaCalculado);
    }
}
