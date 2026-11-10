using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.RegistrarMarcacionFunction;

// Ningun test consume la suscripcion smoke-tests de registro-de-marcacion-creado: esa publicacion la cubre
// por transitividad RegistrarMarcacion_PublicaDiaDepuradoYPersisteMarcacionAdicionada...
// Los casos Retorna400 verifican black-box el validator; no repiten la matriz de su unit test.
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
    private const string TopicProgramacionEntrada = ProgramacionEnLote.Topic;
    private const string TopicDiaDepurado = "dia-depurado";
    private const string SuscripcionSmokeTests = "smoke-tests";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Timestamp valido y fijo para los casos donde lo invalido es el CodigoColaborador, no la fecha:
    // asi el 400 esperado solo puede venir de la regla bajo prueba.
    private static readonly DateTime TimestampValido = new(2026, 4, 20, 8, 0, 0, DateTimeKind.Utc);

    // Stream ID determinista "rdm:{CodigoColaborador}:{Timestamp:yyyyMMddTHHmmss}", en sync con
    // RegistroDeMarcacionAggregateRoot.ComputarStreamId. El timestamp del stream ID es el crudo,
    // antes de normalizar al minuto.
    private static string ComputarStreamId(string codigoColaborador, DateTime timestampCrudo) =>
        $"rdm:{codigoColaborador}:{timestampCrudo:yyyyMMddTHHmmss}";

    private Task<HttpResponseMessage> PostMarcacionAsync(
        string codigoColaborador, DateTime timestamp, string dispositivoId) =>
        _client.PostAsJsonAsync(Ruta, new
        {
            codigoColaborador,
            timestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ss") + "Z",
            tipoMarcacion = "ENTRADA",
            dispositivoId
        }, TestContext.Current.CancellationToken);

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
    public async Task DebeRetornar201YPersistirEvento_CuandoMarcacionEsValida()
    {
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;

        // El handler trunca al minuto antes de emitir; el stream ID usa el timestamp crudo.
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var timestamp = new DateTime(2026, 4, 17, 8, 9, 43, DateTimeKind.Utc);

        var payload = new
        {
            codigoColaborador,
            timestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ss") + "Z",
            tipoMarcacion = "ENTRADA",
            dispositivoId = "[TEST] DEV-SMOKE-001"
        };

        var response = await _client.PostAsJsonAsync(Ruta, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var streamId = ComputarStreamId(codigoColaborador, timestamp);

        var existe = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEvento, Timeout);

        existe.Should().BeTrue(
            $"el evento {TipoEvento} deberia existir en el stream {streamId} tras registrar la marcacion");

        var timestampNormalizado = new DateTime(2026, 4, 17, 8, 9, 0, DateTimeKind.Utc);

        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaControlHoras, streamId, TipoEvento,
            campoJson: "CodigoColaborador", valorJson: codigoColaborador, TimeSpan.FromSeconds(5));

        eventoPersistido.GetProperty("CodigoColaborador").GetString()
            .Should().Be(codigoColaborador);

        eventoPersistido.GetProperty("TipoMarcacion").GetString()
            .Should().Be("ENTRADA");

        eventoPersistido.GetProperty("DispositivoId").GetString()
            .Should().Be("[TEST] DEV-SMOKE-001");

        var timestampPersistido = eventoPersistido.GetProperty("TimestampNormalizado").GetDateTime();
        timestampPersistido.Should().Be(timestampNormalizado);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeRetornar201YPersistirEvento_CuandoMarcacionEsValidaSinCamposOpcionales()
    {
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var timestamp = new DateTime(2026, 4, 17, 9, 30, 15, DateTimeKind.Utc);

        var payload = new
        {
            codigoColaborador,
            timestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ss") + "Z",
            tipoMarcacion = (string?)null,
            dispositivoId = (string?)null
        };

        var response = await _client.PostAsJsonAsync(Ruta, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var streamId = ComputarStreamId(codigoColaborador, timestamp);

        var existe = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEvento, Timeout);

        existe.Should().BeTrue(
            $"el evento {TipoEvento} deberia existir en el stream {streamId} incluso con campos opcionales nulos");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeRetornar201_CuandoMarcacionDuplicadaExacta()
    {
        var ct = TestContext.Current.CancellationToken;

        // Mismo codigo y mismo timestamp -> mismo stream ID: duplicado exacto.
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var timestamp = new DateTime(2026, 4, 17, 10, 0, 0, DateTimeKind.Utc);

        var payload = new
        {
            codigoColaborador,
            timestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ss") + "Z",
            tipoMarcacion = "SALIDA",
            dispositivoId = "[TEST] DEV-SMOKE-DUP"
        };

        var primeraRespuesta = await _client.PostAsJsonAsync(Ruta, payload, ct);
        primeraRespuesta.StatusCode.Should().Be(HttpStatusCode.Created);

        var segundaRespuesta = await _client.PostAsJsonAsync(Ruta, payload, ct);

        // Duplicado silencioso: mismo exito que la creacion, nunca 409.
        segundaRespuesta.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeRetornar400_CuandoBodyEsMalformado()
    {
        var ct = TestContext.Current.CancellationToken;

        var content = new StringContent("esto no es json", System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync(Ruta, content, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeRetornar400_CuandoBodyEsNulo()
    {
        var ct = TestContext.Current.CancellationToken;

        var content = new StringContent("", System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync(Ruta, content, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarMarcacion_Retorna400_CuandoCodigoColaboradorEsVacio()
    {
        var response = await PostMarcacionAsync(
            codigoColaborador: "", TimestampValido, "[TEST] DEV-SMOKE-CA2-VACIO");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarMarcacion_Retorna400_CuandoCodigoColaboradorSonSoloEspacios()
    {
        var response = await PostMarcacionAsync(
            codigoColaborador: "   ", TimestampValido, "[TEST] DEV-SMOKE-CA2-ESPACIOS");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ':' es el separador del stream ID: sin esta regla un codigo con ':' podria fabricar el stream ID
    // de otra combinacion legitima.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarMarcacion_Retorna400_CuandoCodigoColaboradorContieneDosPuntos()
    {
        var response = await PostMarcacionAsync(
            $"EMP:{Guid.CreateVersion7()}", TimestampValido, "[TEST] DEV-SMOKE-CA3");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarMarcacion_Retorna400_CuandoTimestampEsDefault()
    {
        var response = await PostMarcacionAsync(
            Guid.CreateVersion7().ToString(), timestamp: default, "[TEST] DEV-SMOKE-CA4");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Cierra el circuito del contrato RegistroDeMarcacionCreado: marcacion_adicionada solo se persiste si el
    // handler lo publico y el fan-in ActualizarControlDiario lo consumio, asi que prueba que lo proceso el
    // listener de produccion y no un consumidor competidor. El turno previo evita DiaDepurado.Colaborador null.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarMarcacion_PublicaDiaDepuradoYPersisteMarcacionAdicionada_CuandoGeneraNuevoEvento()
    {
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 4, 27);
        var streamId = $"cd:{codigoColaborador}:{fecha:yyyyMMdd}";

        var solicitudId = Guid.CreateVersion7();
        var colaborador = new ResumenColaborador(
            "CC-888777666", codigoColaborador, "[TEST] Smoke DiaDepurado");
        var programacionPayload = new
        {
            SolicitudId = solicitudId,
            Colaborador = colaborador,
            Fechas = new[] { fecha.ToString("yyyy-MM-dd") },
            Jornada = ProgramacionEnLote.Jornada,
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

        await serviceBus.PublishAsync(codigoColaborador, TopicProgramacionEntrada, programacionPayload, solicitudId.ToString());

        var turnoAsignado = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoTurnoDiarioAsignado, Timeout,
            campoJson: "SolicitudId", valorJson: solicitudId.ToString());

        turnoAsignado.Should().BeTrue(
            $"el evento {TipoEventoTurnoDiarioAsignado} deberia existir antes de registrar la marcacion");

        // Purga antes del Act: cualquier mensaje recibido despues es de este test.
        await serviceBus.PurgeAsync(TopicDiaDepurado, SuscripcionSmokeTests);

        var timestamp = new DateTime(fecha, new TimeOnly(8, 0, 0), DateTimeKind.Utc);
        var payload = new
        {
            codigoColaborador,
            timestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ss") + "Z",
            tipoMarcacion = "ENTRADA",
            dispositivoId = "[TEST] DEV-SMOKE-HU108"
        };

        var response = await _client.PostAsJsonAsync(Ruta, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var marcacionAdicionada = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoMarcacionAdicionada, Timeout,
            campoJson: "CodigoColaborador", valorJson: codigoColaborador);

        marcacionAdicionada.Should().BeTrue(
            $"el evento {TipoEventoMarcacionAdicionada} deberia existir en el stream {streamId} tras el POST");

        var diaDepurado = await serviceBus.WaitForMessageAsync<DiaDepurado>(
            TopicDiaDepurado, SuscripcionSmokeTests,
            e => e.CodigoColaborador == codigoColaborador,
            Timeout);

        diaDepurado.Fecha.Should().Be(fecha);
        diaDepurado.CodigoColaborador.Should().Be(codigoColaborador);
        // El turno ya estaba asignado antes de la marcacion, asi que Colaborador lleva la misma terna
        // sembrada: el aggregate la mapea campo a campo, sin componer nada.
        diaDepurado.Colaborador.Should().Be(colaborador);
        diaDepurado.HorasDiscriminadas.Should().NotBeNull(
            "DiaDepurado siempre se emite con HorasDiscriminadas");
        diaDepurado.HorasDiscriminadas.HorasPorConcepto.Should().BeEmpty(
            "una entrada sola deja la franja anomala, sin horas por concepto");
        diaDepurado.NombreTurno.Should().Be("[TEST] Turno HU108");
        diaDepurado.Franjas.Should().ContainSingle(f => f.EsAnomala);
        diaDepurado.Marcaciones.Should().ContainSingle(m => m.Timestamp == timestamp);

        var existeDeadLetter = await serviceBus.ExisteDeadLetterDeEstaCorridaAsync<DiaDepuradoMinimo>(
            TopicDiaDepurado, SuscripcionSmokeTests, e => e.CodigoColaborador == codigoColaborador);

        existeDeadLetter.Should().BeFalse(
            "no deberia haber un dead letter de esta corrida (CodigoColaborador {0}) en '{1}' del topic '{2}'",
            codigoColaborador, SuscripcionSmokeTests, TopicDiaDepurado);

        var existeDeadLetterEnQueue = await serviceBus.ExisteDeadLetterEnQueueDeColaboradorAsync(
            ServiceBusFixture.QueueFanIn, codigoColaborador);

        existeDeadLetterEnQueue.Should().BeFalse(
            "no deberia haber un dead letter del colaborador {0} en el queue '{1}'",
            codigoColaborador, ServiceBusFixture.QueueFanIn);
    }

    // Cada marcacion publica su propio DiaDepurado: se purga con la entrada ya persistida y antes de la
    // salida, para capturar el de la salida. Asercion minima del desglose real; la matematica la cubre unit.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarMarcacion_PublicaDiaDepuradoConDesgloseReal_CuandoMarcacionesCompletanLaFranja()
    {
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 4, 28);
        var streamId = $"cd:{codigoColaborador}:{fecha:yyyyMMdd}";

        var solicitudId = Guid.CreateVersion7();
        var programacionPayload = new
        {
            SolicitudId = solicitudId,
            Colaborador = new ResumenColaborador(
                "CC-777666555", codigoColaborador, "[TEST] Smoke Desglose Real"),
            Fechas = new[] { fecha.ToString("yyyy-MM-dd") },
            Jornada = ProgramacionEnLote.Jornada,
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

        await serviceBus.PublishAsync(codigoColaborador, TopicProgramacionEntrada, programacionPayload, solicitudId.ToString());

        var turnoAsignado = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoTurnoDiarioAsignado, Timeout,
            campoJson: "SolicitudId", valorJson: solicitudId.ToString());

        turnoAsignado.Should().BeTrue(
            $"el evento {TipoEventoTurnoDiarioAsignado} deberia existir antes de registrar las marcaciones");

        var entradaTimestamp = new DateTime(fecha, new TimeOnly(8, 0, 0), DateTimeKind.Utc);
        var entradaResponse = await _client.PostAsJsonAsync(Ruta, new
        {
            codigoColaborador,
            timestamp = entradaTimestamp.ToString("yyyy-MM-ddTHH:mm:ss") + "Z",
            tipoMarcacion = "ENTRADA",
            dispositivoId = "[TEST] DEV-SMOKE-HU181"
        }, ct);
        entradaResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Persistida la entrada, su DiaDepurado ya se publico: el purge lo elimina a el y no al de la salida.
        var entradaPersistida = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoMarcacionAdicionada, Timeout,
            campoJson: "CodigoColaborador", valorJson: codigoColaborador);

        entradaPersistida.Should().BeTrue(
            $"el evento {TipoEventoMarcacionAdicionada} de la entrada deberia existir antes de purgar");

        await serviceBus.PurgeAsync(TopicDiaDepurado, SuscripcionSmokeTests);

        var salidaTimestamp = new DateTime(fecha, new TimeOnly(16, 0, 0), DateTimeKind.Utc);
        var salidaResponse = await _client.PostAsJsonAsync(Ruta, new
        {
            codigoColaborador,
            timestamp = salidaTimestamp.ToString("yyyy-MM-ddTHH:mm:ss") + "Z",
            tipoMarcacion = "SALIDA",
            dispositivoId = "[TEST] DEV-SMOKE-HU181"
        }, ct);
        salidaResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var diaDepurado = await serviceBus.WaitForMessageAsync<DiaDepurado>(
            TopicDiaDepurado, SuscripcionSmokeTests,
            e => e.CodigoColaborador == codigoColaborador,
            Timeout);

        diaDepurado.Fecha.Should().Be(fecha);

        // Clave = Concepto.ToString().
        diaDepurado.HorasDiscriminadas.HorasPorConcepto
            .Should().ContainKey("OrdinariaDiurna");
        diaDepurado.HorasDiscriminadas.HorasPorConcepto["OrdinariaDiurna"]
            .Should().BeGreaterThan(0m,
                "el desglose real discriminado lleva horas ordinarias diurnas (no un payload vacio)");
        diaDepurado.Franjas.Should().ContainSingle(f => !f.EsAnomala);
        diaDepurado.Marcaciones.Should().HaveCount(2);

        var existeDeadLetter = await serviceBus.ExisteDeadLetterDeEstaCorridaAsync<DiaDepuradoMinimo>(
            TopicDiaDepurado, SuscripcionSmokeTests, e => e.CodigoColaborador == codigoColaborador);

        existeDeadLetter.Should().BeFalse(
            "no deberia haber un dead letter de esta corrida (CodigoColaborador {0}) en '{1}' del topic '{2}'",
            codigoColaborador, SuscripcionSmokeTests, TopicDiaDepurado);

        var existeDeadLetterEnQueue = await serviceBus.ExisteDeadLetterEnQueueDeColaboradorAsync(
            ServiceBusFixture.QueueFanIn, codigoColaborador);

        existeDeadLetterEnQueue.Should().BeFalse(
            "no deberia haber un dead letter del colaborador {0} en el queue '{1}'",
            codigoColaborador, ServiceBusFixture.QueueFanIn);
    }

    // Sin turno previo el ControlDiario nace de la marcacion: DiaDepurado conserva CodigoColaborador
    // top-level con Colaborador null.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarMarcacion_PublicaDiaDepuradoConColaboradorNulo_CuandoNoHayTurnoPrevio()
    {
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 4, 29);
        var streamId = $"cd:{codigoColaborador}:{fecha:yyyyMMdd}";

        await serviceBus.PurgeAsync(TopicDiaDepurado, SuscripcionSmokeTests);

        var timestamp = new DateTime(fecha, new TimeOnly(7, 0, 0), DateTimeKind.Utc);
        var payload = new
        {
            codigoColaborador,
            timestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ss") + "Z",
            tipoMarcacion = "ENTRADA",
            dispositivoId = "[TEST] DEV-SMOKE-SIN-TURNO"
        };

        var response = await _client.PostAsJsonAsync(Ruta, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var marcacionAdicionada = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoMarcacionAdicionada, Timeout,
            campoJson: "CodigoColaborador", valorJson: codigoColaborador);

        marcacionAdicionada.Should().BeTrue(
            $"el evento {TipoEventoMarcacionAdicionada} deberia existir en el stream {streamId} tras el POST");

        var diaDepurado = await serviceBus.WaitForMessageAsync<DiaDepurado>(
            TopicDiaDepurado, SuscripcionSmokeTests,
            e => e.CodigoColaborador == codigoColaborador,
            Timeout);

        diaDepurado.Fecha.Should().Be(fecha);
        diaDepurado.Colaborador.Should().BeNull(
            "el dia nacio solo por marcacion, sin turno asignado que aporte la foto del colaborador");
        diaDepurado.NombreTurno.Should().BeNull();
        diaDepurado.Franjas.Should().BeEmpty();
        diaDepurado.Marcaciones.Should().ContainSingle(m => m.Timestamp == timestamp);

        var existeDeadLetter = await serviceBus.ExisteDeadLetterDeEstaCorridaAsync<DiaDepuradoMinimo>(
            TopicDiaDepurado, SuscripcionSmokeTests, e => e.CodigoColaborador == codigoColaborador);

        existeDeadLetter.Should().BeFalse(
            "no deberia haber un dead letter de esta corrida (CodigoColaborador {0}) en '{1}' del topic '{2}'",
            codigoColaborador, SuscripcionSmokeTests, TopicDiaDepurado);

        var existeDeadLetterEnQueue = await serviceBus.ExisteDeadLetterEnQueueDeColaboradorAsync(
            ServiceBusFixture.QueueFanIn, codigoColaborador);

        existeDeadLetterEnQueue.Should().BeFalse(
            "no deberia haber un dead letter del colaborador {0} en el queue '{1}'",
            codigoColaborador, ServiceBusFixture.QueueFanIn);
    }
}
