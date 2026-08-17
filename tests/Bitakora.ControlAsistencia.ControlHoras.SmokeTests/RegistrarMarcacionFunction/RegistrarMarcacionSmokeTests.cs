using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;
using Bitakora.ControlAsistencia.PublicEvents.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.RegistrarMarcacionFunction;

// HU-105: Smoke tests del endpoint POST control-horas/marcaciones
// Verifica camino feliz (202 + persistencia en Postgres), duplicado silencioso (202) y body malformado (400).
// CA-4: duplicado exacto retorna 202 silenciosamente, sin persistir ni publicar de nuevo.
// CA-6: tanto creacion exitosa como duplicado retornan 202 Accepted.
// HU-108: cobertura adicional de los efectos del handler in-process
// AdicionarMarcacionCuandoRegistroDeMarcacionCreado, que tras el POST persiste marcacion_adicionada
// y publica DiaCalculado al topic dia-calculado.
// Issue #270: RegistrarMarcacionCommandHandler ya no publica MarcacionRegistrada (evento de dominio)
// al bus; publica el contrato RegistroDeMarcacionCreado al topic registro-de-marcacion-creado (#274).
// Ningun test consume la suscripcion smoke-tests de ese topic privado: la cobertura de esa
// publicacion/consumo vive en DebePublicarDiaCalculadoYPersistirMarcacionAdicionada... (mas abajo),
// que la verifica de forma mas fuerte por transitividad -- ver el porque en su propio comentario.
// Issue #279: RegistrarMarcacionValidator agrega reglas reales de forma en el borde. Los tests
// RegistrarMarcacion_Retorna400_Cuando* de mas abajo verifican black-box que esas reglas rechazan el
// request contra el entorno desplegado (no repiten la matriz completa del unit test del validator).
// Quedan rojos hasta que el deploy publique el validator en dev: el endpoint desplegado responde 202
// mientras la version anterior siga corriendo. El CI de PR no los ejecuta (solo corre *.Tests).
// Issue #275: protege MarcacionRegistrada con factory y ctores privados, sin efectos nuevos
// observables desde afuera. El truncamiento al minuto solo cambio de casa (handler -> factory) y
// DebeRetornar202YPersistirEvento_CuandoMarcacionEsValida lo sigue verificando end-to-end; el
// CodigoColaborador vacio ya lo rechaza el validator con 400 (#279, arriba) antes de llegar al factory.
// El resto de sus CAs son invariantes internas del tipo (ctores privados, serializacion), cubiertas
// en *.Tests por MarcacionRegistradaTests y MarcacionRegistradaSerializacionTests.
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

    // Issue #279: timestamp valido y fijo para los casos donde lo invalido es el CodigoColaborador, no la
    // fecha; asi el 400 esperado solo puede venir de la regla bajo prueba.
    private static readonly DateTime TimestampValido = new(2026, 4, 20, 8, 0, 0, DateTimeKind.Utc);

    // CA-5: stream ID determinista = "{CodigoColaborador}:{Timestamp:yyyy-MM-ddTHH:mm:ss}"
    // El timestamp que se usa para el stream ID es el crudo (antes de normalizar al minuto).
    private static string ComputarStreamId(string codigoColaborador, DateTime timestampCrudo) =>
        $"{codigoColaborador}:{timestampCrudo:yyyy-MM-ddTHH:mm:ss}";

    // Issue #279: los casos de rechazo por forma solo varian en CodigoColaborador o Timestamp; el resto del
    // payload es identico. Se envia el timestamp con el mismo formato que el resto del archivo.
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
    public async Task DebeRetornar202YPersistirEvento_CuandoMarcacionEsValida()
    {
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;

        // Arrange: timestamp fijo para que el stream ID sea determinista y el test sea reproducible.
        // CA-2: el handler trunca al minuto antes de emitir; el stream ID usa el timestamp crudo.
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var timestamp = new DateTime(2026, 4, 17, 8, 9, 43, DateTimeKind.Utc);

        var payload = new
        {
            codigoColaborador,
            timestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ss") + "Z",
            tipoMarcacion = "ENTRADA",
            dispositivoId = "[TEST] DEV-SMOKE-001"
        };

        // Act
        var response = await _client.PostAsJsonAsync(Ruta, payload, ct);

        // Assert HTTP: 202 Accepted (CA-6)
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Assert persistencia: el evento marcacion_registrada debe existir en el stream
        var streamId = ComputarStreamId(codigoColaborador, timestamp);

        var existe = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEvento, Timeout);

        existe.Should().BeTrue(
            $"el evento {TipoEvento} deberia existir en el stream {streamId} tras registrar la marcacion");

        // Assert detallado: verificar contenido del evento persistido
        // CA-2: TimestampNormalizado = timestamp truncado al minuto (segundos = 0)
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
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var timestamp = new DateTime(2026, 4, 17, 9, 30, 15, DateTimeKind.Utc);

        var payload = new
        {
            codigoColaborador,
            timestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ss") + "Z",
            tipoMarcacion = (string?)null,
            dispositivoId = (string?)null
        };

        // Act
        var response = await _client.PostAsJsonAsync(Ruta, payload, ct);

        // Assert HTTP
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Assert persistencia: el evento debe existir aunque los campos opcionales sean null
        var streamId = ComputarStreamId(codigoColaborador, timestamp);

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

        // Arrange: mismo codigoColaborador y mismo timestamp -> mismo stream ID -> duplicado exacto (CA-4, CA-9)
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var timestamp = new DateTime(2026, 4, 17, 10, 0, 0, DateTimeKind.Utc);

        var payload = new
        {
            codigoColaborador,
            timestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ss") + "Z",
            tipoMarcacion = "SALIDA",
            dispositivoId = "[TEST] DEV-SMOKE-DUP"
        };

        // Act 1: primera marcacion
        var primeraRespuesta = await _client.PostAsJsonAsync(Ruta, payload, ct);
        primeraRespuesta.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Act 2: duplicado exacto (mismo codigoColaborador + mismo timestamp = mismo stream ID)
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

    // Issue #279 CA-2: CodigoColaborador vacio produce 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarMarcacion_Retorna400_CuandoCodigoColaboradorEsVacio()
    {
        var response = await PostMarcacionAsync(
            codigoColaborador: "", TimestampValido, "[TEST] DEV-SMOKE-CA2-VACIO");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Issue #279 CA-2: CodigoColaborador con solo espacios en blanco produce 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarMarcacion_Retorna400_CuandoCodigoColaboradorSonSoloEspacios()
    {
        var response = await PostMarcacionAsync(
            codigoColaborador: "   ", TimestampValido, "[TEST] DEV-SMOKE-CA2-ESPACIOS");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Issue #279 CA-3: CodigoColaborador con ':' produce 400. ComputarStreamId usa ':' como separador entre
    // CodigoColaborador y Timestamp; sin esta regla, un CodigoColaborador con ':' podria fabricar el mismo stream ID
    // que otra combinacion legitima (colision descrita en el Contexto del issue).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarMarcacion_Retorna400_CuandoCodigoColaboradorContieneDosPuntos()
    {
        var response = await PostMarcacionAsync(
            $"EMP:{Guid.CreateVersion7()}", TimestampValido, "[TEST] DEV-SMOKE-CA3");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Issue #279 CA-4: Timestamp con el valor default de DateTime produce 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarMarcacion_Retorna400_CuandoTimestampEsDefault()
    {
        var response = await PostMarcacionAsync(
            Guid.CreateVersion7().ToString(), timestamp: default, "[TEST] DEV-SMOKE-CA4");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // HU-108: tras el POST, el handler in-process AdicionarMarcacionCuandoRegistroDeMarcacionCreado
    // persiste marcacion_adicionada en el stream {codigoColaborador}:{fecha} y publica DiaCalculado.
    // Setup: se publica programacion-turno-diario-solicitada para que el aggregate tenga
    // turno previo, asegurando que DiaCalculado.InformacionColaborador no sea null y se pueda
    // filtrar por CodigoColaborador en la suscripcion smoke-tests.
    // Issue #270: este es el test que cierra el circuito completo del contrato de bus
    // RegistroDeMarcacionCreado -- el POST solo puede llegar a persistir marcacion_adicionada si
    // RegistrarMarcacionCommandHandler publico RegistroDeMarcacionCreado correctamente al topic
    // "registro-de-marcacion-creado" y AdicionarMarcacionCuandoRegistroDeMarcacionCreado lo consumio
    // desde "control-horas-escucha-registro-de-marcacion" (#274). Es un assert black-box mas fuerte
    // que consumir directo la suscripcion smoke-tests de ese topic: prueba que el listener de
    // PRODUCCION (no un consumidor competidor) proceso el mensaje.
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
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 4, 27);
        var streamId = $"{codigoColaborador}:{fecha:yyyy-MM-dd}";

        // Setup: publicar programacion-turno-diario-solicitada y esperar a que ControlHoras
        // persista turno_diario_asignado. Asi el ControlDiario tendra TurnoDiarioAsignado previo
        // antes de procesar la marcacion.
        var solicitudId = Guid.CreateVersion7();
        var programacionPayload = new
        {
            SolicitudId = solicitudId,
            Colaborador = new
            {
                CodigoColaborador = codigoColaborador,
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
            codigoColaborador,
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
            campoJson: "CodigoColaborador", valorJson: codigoColaborador);

        marcacionAdicionada.Should().BeTrue(
            $"el evento {TipoEventoMarcacionAdicionada} deberia existir en el stream {streamId} tras el POST");

        // Assert publicacion: DiaCalculado emitido al topic dia-calculado, filtrado por CodigoColaborador.
        var diaCalculado = await serviceBus.WaitForMessageAsync<DiaCalculado>(
            TopicDiaCalculado, SuscripcionSmokeTests,
            e => e.InformacionColaborador != null && e.InformacionColaborador.CodigoColaborador == codigoColaborador,
            Timeout);

        diaCalculado.Fecha.Should().Be(fecha);
        diaCalculado.InformacionColaborador!.CodigoColaborador.Should().Be(codigoColaborador);
        // Issue #183 CA-6: el payload viaja plano (HorasDiscriminadas) y se deserializo con el
        // serializador POR DEFECTO del fixture (sin resolver custom). Esta marcacion es solo ENTRADA:
        // la franja queda anomala (sin salida) -> sin minutos calculables -> MinutosPorConcepto vacio.
        diaCalculado.HorasDiscriminadas.Should().NotBeNull(
            "DiaCalculado siempre se emite con HorasDiscriminadas");
        diaCalculado.HorasDiscriminadas.MinutosPorConcepto.Should().BeEmpty(
            "una entrada sola deja la franja anomala, sin minutos por concepto");

        // Assert: ausencia de dead letter de ESTA corrida en la suscripcion smoke-tests del topic
        // dia-calculado (issue #223: acotado por CodigoColaborador, no "DLQ globalmente vacio").
        var existeDeadLetter = await serviceBus.ExisteDeadLetterDeEstaCorridaAsync<DiaCalculadoMinimo>(
            TopicDiaCalculado, SuscripcionSmokeTests, e => e.InformacionColaborador?.CodigoColaborador == codigoColaborador);

        existeDeadLetter.Should().BeFalse(
            "no deberia haber un dead letter de esta corrida (CodigoColaborador {0}) en '{1}' del topic '{2}'",
            codigoColaborador, SuscripcionSmokeTests, TopicDiaCalculado);
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
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 4, 28);
        var streamId = $"{codigoColaborador}:{fecha:yyyy-MM-dd}";

        // Setup: turno 08:00-16:00 via Service Bus; esperar a que ControlHoras persista
        // turno_diario_asignado antes de registrar las marcaciones.
        var solicitudId = Guid.CreateVersion7();
        var programacionPayload = new
        {
            SolicitudId = solicitudId,
            Colaborador = new
            {
                CodigoColaborador = codigoColaborador,
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
            codigoColaborador,
            timestamp = entradaTimestamp.ToString("yyyy-MM-ddTHH:mm:ss") + "Z",
            tipoMarcacion = "ENTRADA",
            dispositivoId = "[TEST] DEV-SMOKE-HU181"
        }, ct);
        entradaResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Esperar a que la entrada quede persistida: garantiza que su DiaCalculado ya fue publicado
        // antes de purgar, de modo que el purge elimine el evento de la entrada (no el de la salida).
        var entradaPersistida = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoMarcacionAdicionada, Timeout,
            campoJson: "CodigoColaborador", valorJson: codigoColaborador);

        entradaPersistida.Should().BeTrue(
            $"el evento {TipoEventoMarcacionAdicionada} de la entrada deberia existir antes de purgar");

        // Purge-before-act: limpiar la suscripcion para que el unico DiaCalculado restante de este
        // colaborador sea el que publique la salida (descarta los del turno y la entrada).
        await serviceBus.PurgeAsync(TopicDiaCalculado, SuscripcionSmokeTests);

        // Act 2: SALIDA 16:00. Completa la franja (entrada 08:00 + salida 16:00) -> desglose real.
        var salidaTimestamp = new DateTime(fecha, new TimeOnly(16, 0, 0), DateTimeKind.Utc);
        var salidaResponse = await _client.PostAsJsonAsync(Ruta, new
        {
            codigoColaborador,
            timestamp = salidaTimestamp.ToString("yyyy-MM-ddTHH:mm:ss") + "Z",
            tipoMarcacion = "SALIDA",
            dispositivoId = "[TEST] DEV-SMOKE-HU181"
        }, ct);
        salidaResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Assert publicacion: el DiaCalculado posterior a la salida lleva el desglose real,
        // filtrado por CodigoColaborador (unico por ejecucion).
        var diaCalculado = await serviceBus.WaitForMessageAsync<DiaCalculado>(
            TopicDiaCalculado, SuscripcionSmokeTests,
            e => e.InformacionColaborador != null && e.InformacionColaborador.CodigoColaborador == codigoColaborador,
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

        // Assert: ausencia de dead letter de ESTA corrida en la suscripcion smoke-tests del topic
        // dia-calculado (issue #223: acotado por CodigoColaborador, no "DLQ globalmente vacio").
        var existeDeadLetter = await serviceBus.ExisteDeadLetterDeEstaCorridaAsync<DiaCalculadoMinimo>(
            TopicDiaCalculado, SuscripcionSmokeTests, e => e.InformacionColaborador?.CodigoColaborador == codigoColaborador);

        existeDeadLetter.Should().BeFalse(
            "no deberia haber un dead letter de esta corrida (CodigoColaborador {0}) en '{1}' del topic '{2}'",
            codigoColaborador, SuscripcionSmokeTests, TopicDiaCalculado);
    }
}
