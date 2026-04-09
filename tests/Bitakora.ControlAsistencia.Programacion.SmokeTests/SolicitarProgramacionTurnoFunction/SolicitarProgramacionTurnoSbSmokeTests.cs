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
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebePublicarProgramacionTurnoDiarioSolicitada_CuandoSolicitudEsAceptada()
    {
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
        var correlationId = solicitudId.ToString();
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
            TopicSalida, Suscripcion, correlationId, Timeout);

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
    }
}
