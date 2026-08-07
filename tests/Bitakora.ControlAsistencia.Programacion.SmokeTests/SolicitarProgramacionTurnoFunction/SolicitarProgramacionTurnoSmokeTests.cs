using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.SolicitarProgramacionTurnoFunction;

public class SolicitarProgramacionTurnoSmokeTests(ApiFixture api, ServiceBusFixture serviceBus)
{
    private readonly HttpClient _client = api.Client;

    private const string TopicSalida = "programacion-turno-diario-solicitada";
    private const string Suscripcion = "smoke-tests";
    private const string SuscripcionConsumidor = "control-horas-escucha-programacion";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static object PayloadValido(Guid? id = null, Guid? turnoId = null) => new
    {
        id = id ?? Guid.CreateVersion7(),
        turnoId = turnoId ?? Guid.CreateVersion7(),
        empleado = new
        {
            empleadoId = Guid.CreateVersion7().ToString(),
            tipoIdentificacion = "CC",
            numeroIdentificacion = "123456789",
            nombres = "[TEST] Juan Carlos",
            apellidos = "[TEST] Perez Lopez"
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
        var empleadoId = Guid.CreateVersion7().ToString();
        var fecha1 = "2026-04-15";
        var fecha2 = "2026-04-16";
        var payload = new
        {
            id = solicitudId,
            turnoId,
            empleado = new
            {
                empleadoId,
                tipoIdentificacion = "CC",
                numeroIdentificacion = "555666777",
                nombres = "[TEST] Smoke ServiceBus",
                apellidos = "[TEST] Publicacion"
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

        // Verificar datos del empleado y turno en uno de los eventos. El payload del empleado es
        // DetalleEmpleado (PrivateEvents): con la paridad de campos el JSON del cable no cambia,
        // asi que este smoke test tambien evidencia la compatibilidad del despliegue rolling.
        var empleadoEsperado = new DetalleEmpleado(
            empleadoId, "CC", "555666777", "[TEST] Smoke ServiceBus", "[TEST] Publicacion");
        evento1.Empleado.Should().Be(empleadoEsperado);

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
        var empleadoId = Guid.CreateVersion7().ToString();
        var sedeId = "SEDE-01";
        var sedeNombre = "[TEST] Sede Principal";
        var payload = new
        {
            id = solicitudId,
            turnoId,
            empleado = new
            {
                empleadoId,
                tipoIdentificacion = "CC",
                numeroIdentificacion = "111222333",
                nombres = "[TEST] Smoke Sede",
                apellidos = "[TEST] Publicacion"
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
            empleado = new
            {
                empleadoId = Guid.CreateVersion7().ToString(),
                tipoIdentificacion = "CC",
                numeroIdentificacion = "123456789",
                nombres = "[TEST] Juan",
                apellidos = "[TEST] Perez"
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
            empleado = new
            {
                empleadoId = Guid.CreateVersion7().ToString(),
                tipoIdentificacion = "CC",
                numeroIdentificacion = "123456789",
                nombres = "[TEST] Juan",
                apellidos = "[TEST] Perez"
            },
            fechas = new[] { "2025-08-01" }
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_DebeRetornar400_CuandoEmpleadoTieneCamposVacios()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            id = Guid.CreateVersion7(),
            turnoId = Guid.CreateVersion7(),
            empleado = new
            {
                empleadoId = "",
                tipoIdentificacion = "",
                numeroIdentificacion = "",
                nombres = "",
                apellidos = ""
            },
            fechas = new[] { "2025-08-01" }
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_DebeRetornar400_CuandoEmpleadoEsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            id = Guid.CreateVersion7(),
            turnoId = Guid.CreateVersion7(),
            empleado = (object?)null,
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
            empleado = new
            {
                empleadoId = Guid.CreateVersion7().ToString(),
                tipoIdentificacion = "CC",
                numeroIdentificacion = "123456789",
                nombres = "[TEST] Juan",
                apellidos = "[TEST] Perez"
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
            empleado = new
            {
                empleadoId = Guid.CreateVersion7().ToString(),
                tipoIdentificacion = "CC",
                numeroIdentificacion = "123456789",
                nombres = "[TEST] Juan",
                apellidos = "[TEST] Perez"
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
            empleado = new
            {
                empleadoId = Guid.CreateVersion7().ToString(),
                tipoIdentificacion = "CC",
                numeroIdentificacion = "123456789",
                nombres = "[TEST] Juan",
                apellidos = "[TEST] Perez"
            },
            fechas = new[] { "2025-08-01" },
            sede = new { id = "SEDE-01", nombre = "   " }
        };

        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
