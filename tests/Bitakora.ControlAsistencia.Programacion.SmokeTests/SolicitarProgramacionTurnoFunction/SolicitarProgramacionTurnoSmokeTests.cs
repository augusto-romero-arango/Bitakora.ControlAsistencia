using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.SolicitarProgramacionTurnoFunction;

public class SolicitarProgramacionTurnoSmokeTests(
    ApiFixture api, ServiceBusFixture serviceBus, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string TopicSalida = "programacion-turno-diario-solicitada";
    private const string Suscripcion = "smoke-tests";
    private const string SuscripcionConsumidor = "control-horas-escucha-programacion";
    private const string SchemaProgramacion = "programacion";
    private const string TipoEventoProgramacionSolicitada = "programacion_turno_solicitada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Formas minimas del evento persistido, para asertar sobre el JSON de mt_events sin referenciar
    // Programacion.DomainEvents desde los smoke tests (mismo criterio que DeadLetterMinimos y que
    // CrearTurnoSmokeTests). Solo declaran los campos que este test verifica; leerlas de forma
    // case-insensitive deja la politica de nombres del serializador fuera de la asercion -- lo que
    // se verifica es el DATO que quedo grabado, no como el host llama a la clave.
    private sealed record SedeMinima(string Id, string Nombre);
    private sealed record FranjaMinima(SedeMinima? Sede);
    private sealed record TurnoMinimo(IReadOnlyList<FranjaMinima> FranjasOrdinarias);
    private sealed record SolicitudMinima(TurnoMinimo DetalleTurno, SedeMinima? Sede);

    private static readonly JsonSerializerOptions OpcionesLectura = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Turno de catalogo con DOS franjas: la primera trae sede prearmada, la segunda no -- el
    // arrange que ejercita la cascada franja por franja (CA-1). Horarios sin solapamiento
    // (TurnoCreado.Crear valida solapamiento entre ordinarias).
    private static object TurnoConFranjasMixtasPayload(Guid turnoId, string sedeId, string sedeNombre) => new
    {
        turnoId,
        nombre = "[TEST] Turno Mixto Cascada",
        ordinarias = new object[]
        {
            new
            {
                inicio = "06:00:00",
                fin = "10:00:00",
                descansos = Array.Empty<object>(),
                extras = Array.Empty<object>(),
                sede = new { id = sedeId, nombre = sedeNombre }
            },
            new
            {
                inicio = "14:00:00",
                fin = "18:00:00",
                descansos = Array.Empty<object>(),
                extras = Array.Empty<object>()
            }
        }
    };

    private static object PayloadValido(Guid? id = null, Guid? turnoId = null) => new
    {
        id = id ?? Guid.CreateVersion7(),
        turnoId = turnoId ?? Guid.CreateVersion7(),
        colaborador = new
        {
            identificacion = "CC-123456789",
            codigoColaborador = Guid.CreateVersion7().ToString(),
            nombreCompleto = "[TEST] Juan Carlos Perez Lopez"
        },
        fechas = new[] { "2025-08-01", "2025-08-02" }
    };

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebePublicarProgramacionTurnoDiarioSolicitada_CuandoSolicitudEsAceptada()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        // Arrange: purgar mensajes preexistentes de ejecuciones anteriores
        await serviceBus.PurgeAsync(TopicSalida, Suscripcion);

        // Arrange: crear turno en catalogo
        var turnoId = Guid.CreateVersion7();
        var turnoPayload = new
        {
            turnoId,
            nombre = "[TEST] Turno Smoke SB",
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
        var crearTurnoResponse = await _client.PostAsJsonAsync("/api/programacion/turnos", turnoPayload, ct);
        crearTurnoResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Arrange: preparar solicitud con dos fechas para verificar emision de un evento por fecha
        var solicitudId = Guid.CreateVersion7();
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var identificacion = "CC-555666777";
        var nombreCompleto = "[TEST] Smoke ServiceBus Publicacion";
        var fecha1 = "2026-04-15";
        var fecha2 = "2026-04-16";
        var payload = new
        {
            id = solicitudId,
            turnoId,
            colaborador = new
            {
                identificacion,
                codigoColaborador,
                nombreCompleto
            },
            fechas = new[] { fecha1, fecha2 }
        };

        // Act: enviar solicitud via HTTP
        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes", payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Assert: consumir los 2 eventos publicados desde la suscripcion smoke-tests
        var evento1 = await serviceBus.WaitForMessageAsync<ProgramacionTurnoDiarioSolicitada>(
            TopicSalida, Suscripcion, e => e.SolicitudId == solicitudId, Timeout);
        var evento2 = await serviceBus.WaitForMessageAsync<ProgramacionTurnoDiarioSolicitada>(
            TopicSalida, Suscripcion, e => e.SolicitudId == solicitudId, Timeout);

        // Verificar que las fechas recibidas corresponden a las enviadas (sin importar orden)
        new[] { evento1.Fecha, evento2.Fecha }.Should()
            .BeEquivalentTo(new[] { DateOnly.Parse(fecha1), DateOnly.Parse(fecha2) });

        // Issue #436: el body ya envia la terna y el handler la pasa TAL CUAL al bus. Este oraculo
        // acredita end-to-end ese pass-through: los tres valores que salen al bus son, literalmente,
        // los tres que entraron por HTTP. Si un mapeo permutara dos campos de la terna (todos
        // string, ninguna diferencia de tipo que delate la permutacion), el defecto aparece aqui.
        var colaboradorEsperado = new ResumenColaborador(
            identificacion, codigoColaborador, nombreCompleto);
        evento1.Colaborador.Should().Be(colaboradorEsperado);

        evento1.DetalleTurno.Should().NotBeNull();
        evento1.DetalleTurno.Nombre.Should().Be("[TEST] Turno Smoke SB");
        evento1.DetalleTurno.FranjasOrdinarias.Should().HaveCount(1);

        // Issue #331 CA-2: la solicitud no incluye sede -> el comportamiento actual (anterior al
        // issue) queda intacto: el evento diario publicado lleva Sede = null.
        evento1.Sede.Should().BeNull();
        evento2.Sede.Should().BeNull();

        // Assert: verificar ausencia de dead letter de ESTA corrida en la suscripcion del consumidor real
        // (issue #223: acotado por SolicitudId, no "DLQ globalmente vacio" - residuales de otras
        // corridas no deben tumbar este test).
        await Task.Delay(TimeSpan.FromSeconds(5), ct);

        var existeDeadLetter = await serviceBus.ExisteDeadLetterDeEstaCorridaAsync<ProgramacionTurnoDiarioSolicitadaMinimo>(
            TopicSalida, SuscripcionConsumidor, e => e.SolicitudId == solicitudId);

        existeDeadLetter.Should().BeFalse(
            "no deberia haber un dead letter de esta corrida (SolicitudId {0}) en '{1}' - si lo hay, el consumidor fallo al procesar el evento",
            solicitudId, SuscripcionConsumidor);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_PublicaElEventoDiarioConLaSede_CuandoLaSolicitudIncluyeSede()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        // Arrange: purgar mensajes preexistentes de ejecuciones anteriores
        await serviceBus.PurgeAsync(TopicSalida, Suscripcion);

        // Arrange: crear turno en catalogo
        var turnoId = Guid.CreateVersion7();
        var turnoPayload = new
        {
            turnoId,
            nombre = "[TEST] Turno Smoke Sede",
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
        var crearTurnoResponse = await _client.PostAsJsonAsync("/api/programacion/turnos", turnoPayload, ct);
        crearTurnoResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Arrange: solicitud con sede -- issue #331 CA-1
        var solicitudId = Guid.CreateVersion7();
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var sedeId = "SEDE-01";
        var sedeNombre = "[TEST] Sede Principal";
        var payload = new
        {
            id = solicitudId,
            turnoId,
            colaborador = new
            {
                identificacion = "CC-111222333",
                codigoColaborador,
                nombreCompleto = "[TEST] Smoke Sede Publicacion"
            },
            fechas = new[] { "2026-05-01" },
            sede = new { id = sedeId, nombre = sedeNombre }
        };

        // Act: enviar solicitud via HTTP
        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes", payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Assert: el evento diario publicado lleva la sede resuelta por el cliente
        var evento = await serviceBus.WaitForMessageAsync<ProgramacionTurnoDiarioSolicitada>(
            TopicSalida, Suscripcion, e => e.SolicitudId == solicitudId, Timeout);

        var sedeEsperada = new DetalleSede(sedeId, sedeNombre);
        evento.Sede.Should().Be(sedeEsperada);

        // Issue #341 CA-3: el turno del catalogo NO trae sedes prearmadas en sus franjas -> la
        // cascada (franja.Sede ?? sedePorDefecto) aplica la sede de la solicitud a la UNICA franja.
        evento.DetalleTurno.FranjasOrdinarias.Should().HaveCount(1);
        evento.DetalleTurno.FranjasOrdinarias[0].Sede.Should().Be(sedeEsperada);

        // Assert: el campo nuevo es aditivo y TOLERANTE para el consumidor real -- ControlHoras
        // todavia no conoce "sede" (lo consumira en #336) y debe seguir procesando el mensaje sin
        // dead-letter. Es la unica evidencia end-to-end de esa tolerancia: el test de arriba
        // (sin sede) nunca pone el campo nuevo en el cable.
        await Task.Delay(TimeSpan.FromSeconds(5), ct);

        var existeDeadLetter = await serviceBus.ExisteDeadLetterDeEstaCorridaAsync<ProgramacionTurnoDiarioSolicitadaMinimo>(
            TopicSalida, SuscripcionConsumidor, e => e.SolicitudId == solicitudId);

        existeDeadLetter.Should().BeFalse(
            "el campo 'sede' es aditivo: ControlHoras debe ignorarlo sin fallar (SolicitudId {0}, suscripcion '{1}')",
            solicitudId, SuscripcionConsumidor);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_ConservaLaSedeDeLaFranjaYAplicaLaSedePorDefecto_CuandoElTurnoTraeFranjasMixtas()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        // Arrange: purgar mensajes preexistentes de ejecuciones anteriores
        await serviceBus.PurgeAsync(TopicSalida, Suscripcion);

        // Arrange: crear turno en catalogo con franjas mixtas (una con sede prearmada, otra sin)
        var turnoId = Guid.CreateVersion7();
        var sedePrincipal = new DetalleSede("SEDE-01", "[TEST] Sede Principal");
        var sedeSuba = new DetalleSede("SEDE-SUBA", "[TEST] Suba");
        var crearTurnoResponse = await _client.PostAsJsonAsync(
            "/api/programacion/turnos",
            TurnoConFranjasMixtasPayload(turnoId, sedeSuba.Id, sedeSuba.Nombre), ct);
        crearTurnoResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Arrange: solicitud CON sede -- issue #341 CA-1
        var solicitudId = Guid.CreateVersion7();
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var payload = new
        {
            id = solicitudId,
            turnoId,
            colaborador = new
            {
                identificacion = "CC-444555666",
                codigoColaborador,
                nombreCompleto = "[TEST] Smoke Cascada Publicacion"
            },
            fechas = new[] { "2026-06-01" },
            sede = new { id = sedePrincipal.Id, nombre = sedePrincipal.Nombre }
        };

        // Act: enviar solicitud via HTTP
        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes", payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Assert: cada franja resuelve su cascada de forma independiente. La franja1 conserva su
        // sede propia del catalogo (le gana al default); la franja2 (sin sede propia) adopta la
        // sede de la solicitud.
        var evento = await serviceBus.WaitForMessageAsync<ProgramacionTurnoDiarioSolicitada>(
            TopicSalida, Suscripcion, e => e.SolicitudId == solicitudId, Timeout);

        evento.Sede.Should().Be(sedePrincipal);
        evento.DetalleTurno.FranjasOrdinarias.Should().HaveCount(2);
        evento.DetalleTurno.FranjasOrdinarias[0].Sede.Should().Be(sedeSuba);
        evento.DetalleTurno.FranjasOrdinarias[1].Sede.Should().Be(sedePrincipal);

        // Assert: tolerancia del consumidor real ante el campo aditivo (sin dead letter de esta corrida)
        await Task.Delay(TimeSpan.FromSeconds(5), ct);

        var existeDeadLetter = await serviceBus.ExisteDeadLetterDeEstaCorridaAsync<ProgramacionTurnoDiarioSolicitadaMinimo>(
            TopicSalida, SuscripcionConsumidor, e => e.SolicitudId == solicitudId);

        existeDeadLetter.Should().BeFalse(
            "no deberia haber un dead letter de esta corrida (SolicitudId {0}) en '{1}' - si lo hay, el consumidor fallo al procesar el evento",
            solicitudId, SuscripcionConsumidor);
    }

    // El handler tiene DOS efectos secundarios y los tests de arriba solo cubren uno (la
    // publicacion al bus). Este cubre el otro: la persistencia en el event store
    // (SolicitarProgramacionTurnoCommandHandler -> IEventStore.StartStream), que es lo que CA-1
    // pide verificar "en el evento persistido" ademas de "en cada evento diario del bus".
    // mt_events es la unica ventana black-box a lo que quedo grabado -- y cierra el riesgo real:
    // que la sede efectiva llegue bien al bus pero se pierda en silencio en el JSON persistido,
    // con un 202 igual de verde. Mismo patron que CrearTurnoSmokeTests para turno_creado (#335).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_PersisteLaSedeEfectivaDeCadaFranja_CuandoElTurnoTraeFranjasMixtas()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;

        // Arrange: turno de catalogo con franjas mixtas (una con sede prearmada, otra sin)
        var turnoId = Guid.CreateVersion7();
        var sedeSuba = new SedeMinima("SEDE-SUBA", "[TEST] Suba");
        var sedePrincipal = new SedeMinima("SEDE-01", "[TEST] Sede Principal");
        var crearTurnoResponse = await _client.PostAsJsonAsync(
            "/api/programacion/turnos",
            TurnoConFranjasMixtasPayload(turnoId, sedeSuba.Id, sedeSuba.Nombre), ct);
        crearTurnoResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var solicitudId = Guid.CreateVersion7();
        var payload = new
        {
            id = solicitudId,
            turnoId,
            colaborador = new
            {
                identificacion = "CC-888999000",
                codigoColaborador = Guid.CreateVersion7().ToString(),
                nombreCompleto = "[TEST] Smoke Persistencia Cascada"
            },
            fechas = new[] { "2026-06-03" },
            sede = new { id = sedePrincipal.Id, nombre = sedePrincipal.Nombre }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes", payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // SolicitudProgramacionAggregateRoot.Apply asigna Id = evento.Id.ToString() -- el stream id
        // es el guid canonico de la solicitud, sin formato explicito (MEF-ADR-0037).
        var streamId = solicitudId.ToString();

        var json = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaProgramacion, streamId, TipoEventoProgramacionSolicitada,
            campoJson: "Id", valorJson: streamId, Timeout);

        var eventoPersistido = json.Deserialize<SolicitudMinima>(OpcionesLectura);
        eventoPersistido.Should().NotBeNull();

        // CA-1: cada franja quedo grabada con SU sede efectiva -- la prearmada le gana al default.
        eventoPersistido!.DetalleTurno.FranjasOrdinarias.Should().HaveCount(2);
        eventoPersistido.DetalleTurno.FranjasOrdinarias[0].Sede.Should().Be(sedeSuba);
        eventoPersistido.DetalleTurno.FranjasOrdinarias[1].Sede.Should().Be(sedePrincipal);

        // CA-3: la cascada NO altera el nivel de la solicitud -- ahi sigue LO SOLICITADO.
        eventoPersistido.Sede.Should().Be(sedePrincipal);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_ConservaLaSedeDelCatalogo_CuandoLaSolicitudNoIncluyeSede()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        // Arrange: purgar mensajes preexistentes de ejecuciones anteriores
        await serviceBus.PurgeAsync(TopicSalida, Suscripcion);

        // Arrange: crear turno en catalogo con sede PREARMADA en su unica franja.
        var turnoId = Guid.CreateVersion7();
        var sedeCatalogo = new DetalleSede("SEDE-CENTRO", "[TEST] Centro");
        var turnoPayload = new
        {
            turnoId,
            nombre = "[TEST] Turno Con Sede Prearmada",
            ordinarias = new[]
            {
                new
                {
                    inicio = "08:00:00",
                    fin = "16:00:00",
                    descansos = Array.Empty<object>(),
                    extras = Array.Empty<object>(),
                    sede = new { id = sedeCatalogo.Id, nombre = sedeCatalogo.Nombre }
                }
            }
        };
        var crearTurnoResponse = await _client.PostAsJsonAsync("/api/programacion/turnos", turnoPayload, ct);
        crearTurnoResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Arrange: solicitud SIN sede -- issue #341 CA-2: la franja con sede del catalogo la
        // conserva (el catalogo le gana al "sin sede" de la solicitud tambien).
        var solicitudId = Guid.CreateVersion7();
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var payload = new
        {
            id = solicitudId,
            turnoId,
            colaborador = new
            {
                identificacion = "CC-777888999",
                codigoColaborador,
                nombreCompleto = "[TEST] Smoke Prearmada Publicacion"
            },
            fechas = new[] { "2026-06-02" }
        };

        // Act: enviar solicitud via HTTP
        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes", payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Assert: el nivel de solicitud sigue siendo null (lo solicitado), pero la franja conserva
        // la sede del catalogo (la verdad efectiva ya resuelta por la cascada).
        var evento = await serviceBus.WaitForMessageAsync<ProgramacionTurnoDiarioSolicitada>(
            TopicSalida, Suscripcion, e => e.SolicitudId == solicitudId, Timeout);

        evento.Sede.Should().BeNull();
        evento.DetalleTurno.FranjasOrdinarias.Should().HaveCount(1);
        evento.DetalleTurno.FranjasOrdinarias[0].Sede.Should().Be(sedeCatalogo);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_DebeRetornar409_CuandoSolicitudYaExiste()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        // Arrange: purgar mensajes preexistentes
        await serviceBus.PurgeAsync(TopicSalida, Suscripcion);

        // Arrange: crear turno en catalogo
        var turnoId = Guid.CreateVersion7();
        var turnoPayload = new
        {
            turnoId,
            nombre = "[TEST] Turno para Duplicado",
            ordinarias = new[]
            {
                new
                {
                    inicio = "07:00:00",
                    fin = "15:00:00",
                    descansos = Array.Empty<object>(),
                    extras = Array.Empty<object>()
                }
            }
        };
        await _client.PostAsJsonAsync("/api/programacion/turnos", turnoPayload, ct);

        var solicitudId = Guid.CreateVersion7();
        var payload = PayloadValido(id: solicitudId, turnoId: turnoId);

        // Act 1: primera solicitud (exitosa)
        var primeraRespuesta = await _client.PostAsJsonAsync("/api/programacion/solicitudes", payload, ct);
        primeraRespuesta.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Assert: consumir los 2 eventos ProgramacionTurnoDiarioSolicitada publicados
        var evento1 = await serviceBus.WaitForMessageAsync<ProgramacionTurnoDiarioSolicitada>(
            TopicSalida, Suscripcion, e => e.SolicitudId == solicitudId, Timeout);
        var evento2 = await serviceBus.WaitForMessageAsync<ProgramacionTurnoDiarioSolicitada>(
            TopicSalida, Suscripcion, e => e.SolicitudId == solicitudId, Timeout);

        // Verificar que las fechas recibidas corresponden a las enviadas
        new[] { evento1.Fecha, evento2.Fecha }.Should()
            .BeEquivalentTo(new[] { DateOnly.Parse("2025-08-01"), DateOnly.Parse("2025-08-02") });

        // Act 2: solicitud duplicada
        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_DebeRetornar404_CuandoTurnoNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadValido(turnoId: Guid.CreateVersion7());

        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_DebeRetornar400_CuandoIdEsGuidVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            id = Guid.Empty,
            turnoId = Guid.CreateVersion7(),
            colaborador = new
            {
                identificacion = "CC-123456789",
                codigoColaborador = Guid.CreateVersion7().ToString(),
                nombreCompleto = "[TEST] Juan Perez"
            },
            fechas = new[] { "2025-08-01" }
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_DebeRetornar400_CuandoTurnoIdEsGuidVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            id = Guid.CreateVersion7(),
            turnoId = Guid.Empty,
            colaborador = new
            {
                identificacion = "CC-123456789",
                codigoColaborador = Guid.CreateVersion7().ToString(),
                nombreCompleto = "[TEST] Juan Perez"
            },
            fechas = new[] { "2025-08-01" }
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_DebeRetornar400_CuandoColaboradorTieneCamposVacios()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            id = Guid.CreateVersion7(),
            turnoId = Guid.CreateVersion7(),
            colaborador = new
            {
                identificacion = "",
                codigoColaborador = "",
                nombreCompleto = ""
            },
            fechas = new[] { "2025-08-01" }
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_DebeRetornar400_CuandoColaboradorEsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            id = Guid.CreateVersion7(),
            turnoId = Guid.CreateVersion7(),
            colaborador = (object?)null,
            fechas = new[] { "2025-08-01" }
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_DebeRetornar400_CuandoFechasEstaVacia()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            id = Guid.CreateVersion7(),
            turnoId = Guid.CreateVersion7(),
            colaborador = new
            {
                identificacion = "CC-123456789",
                codigoColaborador = Guid.CreateVersion7().ToString(),
                nombreCompleto = "[TEST] Juan Perez"
            },
            fechas = Array.Empty<string>()
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Issue #331 CA-3: sede presente pero con Id vacio se rechaza con 400, sin emitir eventos.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_Retorna400_CuandoSedeTieneIdVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            id = Guid.CreateVersion7(),
            turnoId = Guid.CreateVersion7(),
            colaborador = new
            {
                identificacion = "CC-123456789",
                codigoColaborador = Guid.CreateVersion7().ToString(),
                nombreCompleto = "[TEST] Juan Perez"
            },
            fechas = new[] { "2025-08-01" },
            sede = new { id = "", nombre = "[TEST] Sede Principal" }
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Issue #331 CA-3: sede presente pero con Nombre en blanco se rechaza con 400, sin emitir eventos.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_Retorna400_CuandoSedeTieneNombreEnBlanco()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            id = Guid.CreateVersion7(),
            turnoId = Guid.CreateVersion7(),
            colaborador = new
            {
                identificacion = "CC-123456789",
                codigoColaborador = Guid.CreateVersion7().ToString(),
                nombreCompleto = "[TEST] Juan Perez"
            },
            fechas = new[] { "2025-08-01" },
            sede = new { id = "SEDE-01", nombre = "   " }
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
