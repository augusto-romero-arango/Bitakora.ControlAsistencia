using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction;

public class AsignarTurnoViaSbSmokeTests(ServiceBusFixture serviceBus, PostgresFixture postgres)
{
    private const string TopicEntrada = "programacion-turno-diario-solicitada";
    private const string SchemaControlHoras = "control_horas";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeAsignarTurnoDiario_CuandoSeBusPublicaProgramacionTurnoDiarioSolicitada()
    {
        // Arrange
        var correlationId = Guid.CreateVersion7().ToString();
        var solicitudId = Guid.CreateVersion7();
        var empleadoId = Guid.CreateVersion7().ToString();
        var fecha = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));

        var evento = new
        {
            SolicitudId = solicitudId,
            Empleado = new
            {
                EmpleadoId = empleadoId,
                TipoIdentificacion = "CC",
                NumeroIdentificacion = "999888777",
                Nombres = "[TEST] Smoke ServiceBus",
                Apellidos = "[TEST] Verificacion"
            },
            Fecha = fecha.ToString("yyyy-MM-dd"),
            DetalleTurno = new
            {
                Nombre = "[TEST] Turno Smoke SB",
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

        // Act: publicar al topic de Service Bus
        await serviceBus.PublishAsync(TopicEntrada, evento, correlationId);

        // Assert: verificar que el evento TurnoDiarioAsignado fue persistido en PostgreSQL
        var streamId = $"{empleadoId}:{fecha:yyyy-MM-dd}";
        var tipoEvento = "turno_diario_asignado";

        var existe = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, tipoEvento, Timeout);

        existe.Should().BeTrue(
            $"el evento {tipoEvento} deberia existir en el stream {streamId} despues de publicar al topic {TopicEntrada}");

        // Assert detallado: obtener los eventos y verificar campos
        var eventos = await postgres.ObtenerEventosAsync<JsonElement>(
            SchemaControlHoras, streamId, TimeSpan.FromSeconds(5));

        eventos.Should().NotBeEmpty();

        var ultimo = eventos[^1];
        ultimo.GetProperty("SolicitudId").GetGuid().Should().Be(solicitudId);
        ultimo.GetProperty("InformacionEmpleado").GetProperty("EmpleadoId").GetString()
            .Should().Be(empleadoId);
        ultimo.GetProperty("Fecha").GetString().Should().Be(fecha.ToString("yyyy-MM-dd"));
        ultimo.GetProperty("DetalleTurno").GetProperty("Nombre").GetString()
            .Should().Be("[TEST] Turno Smoke SB");
    }
}
