using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.Empleados.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Programacion.Eventos;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;
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

        // Arrange: preparar solicitud con una sola fecha para simplificar verificacion
        var solicitudId = Guid.CreateVersion7();
        var empleadoId = Guid.CreateVersion7().ToString();
        var fecha = "2026-04-15";
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
            fechas = new[] { fecha }
        };

        // Act: enviar solicitud via HTTP
        var response = await _client.PostAsJsonAsync("/api/programacion/solicitudes", payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Assert: consumir el evento publicado desde la suscripcion smoke-tests
        var eventoRecibido = await serviceBus.WaitForMessageAsync<ProgramacionTurnoDiarioSolicitada>(
            TopicSalida, Suscripcion, e => e.SolicitudId == solicitudId, Timeout);

        eventoRecibido.SolicitudId.Should().Be(solicitudId);
        eventoRecibido.Fecha.Should().Be(DateOnly.Parse(fecha));

        var empleadoEsperado = new InformacionEmpleado(
            empleadoId, "CC", "555666777", "[TEST] Smoke ServiceBus", "[TEST] Publicacion");
        eventoRecibido.Empleado.Should().Be(empleadoEsperado);

        eventoRecibido.DetalleTurno.Should().NotBeNull();
        eventoRecibido.DetalleTurno.Nombre.Should().Be("[TEST] Turno Smoke SB");
        eventoRecibido.DetalleTurno.FranjasOrdinarias.Should().HaveCount(1);

        // Assert: verificar ausencia de dead letters en la suscripcion del consumidor real
        await Task.Delay(TimeSpan.FromSeconds(5), ct);

        var deadLetters = await serviceBus.PeekDeadLetterMessagesAsync(
            TopicSalida, SuscripcionConsumidor);

        deadLetters.Should().BeEmpty(
            "no deberia haber mensajes en dead letter de '{0}' - si los hay, el consumidor fallo al procesar el evento",
            SuscripcionConsumidor);
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
}
