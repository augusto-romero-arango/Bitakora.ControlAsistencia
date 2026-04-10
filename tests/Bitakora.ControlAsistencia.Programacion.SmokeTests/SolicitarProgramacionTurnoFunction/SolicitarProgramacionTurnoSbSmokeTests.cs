using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.Empleados.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Programacion.Eventos;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.SolicitarProgramacionTurnoFunction;

public class SolicitarProgramacionTurnoSbSmokeTests(ApiFixture api, ServiceBusFixture serviceBus)
{
    private readonly HttpClient _client = api.Client;

    private const string TopicSalida = "programacion-turno-diario-solicitada";
    private const string Suscripcion = "smoke-tests";
    private const string SuscripcionConsumidor = "control-horas-escucha-programacion";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebePublicarProgramacionTurnoDiarioSolicitada_CuandoSolicitudEsAceptada()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

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
        // La FA publica via Wolverine, que no establece CorrelationId.
        // Matching por contenido (SolicitudId) para aislar el mensaje de este test.
        var eventoRecibido = await serviceBus.WaitForMessageAsync<ProgramacionTurnoDiarioSolicitada>(
            TopicSalida, Suscripcion, e => e.SolicitudId == solicitudId, Timeout);

        eventoRecibido.Should().NotBeNull(
            "la Function App deberia publicar ProgramacionTurnoDiarioSolicitada al topic de Service Bus");

        eventoRecibido!.SolicitudId.Should().Be(solicitudId);
        eventoRecibido.Fecha.Should().Be(DateOnly.Parse(fecha));

        var empleadoEsperado = new InformacionEmpleado(
            empleadoId, "CC", "555666777", "[TEST] Smoke ServiceBus", "[TEST] Publicacion");
        eventoRecibido.Empleado.Should().Be(empleadoEsperado);

        eventoRecibido.DetalleTurno.Should().NotBeNull();
        eventoRecibido.DetalleTurno.Nombre.Should().Be("[TEST] Turno Smoke SB");
        eventoRecibido.DetalleTurno.FranjasOrdinarias.Should().HaveCount(1);

        // Assert: verificar ausencia de dead letters en la suscripcion del consumidor real.
        // Esperar a que el consumidor haya tenido tiempo de procesar el mensaje.
        await Task.Delay(TimeSpan.FromSeconds(5), ct);

        var deadLetters = await serviceBus.PeekDeadLetterMessagesAsync(
            TopicSalida, SuscripcionConsumidor);

        deadLetters.Should().BeEmpty(
            "no deberia haber mensajes en dead letter de '{0}' - si los hay, el consumidor fallo al procesar el evento",
            SuscripcionConsumidor);
    }
}
