using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ValueObjects;
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
        var fecha = new DateOnly(2026, 4, 9);

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
            SchemaControlHoras, streamId, tipoEvento, Timeout,
            campoJson: "SolicitudId", valorJson: solicitudId.ToString());

        existe.Should().BeTrue(
            $"el evento {tipoEvento} con SolicitudId {solicitudId} deberia existir en el stream {streamId}");

        // Assert detallado: obtener el evento especifico y comparar value objects
        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaControlHoras, streamId, tipoEvento,
            "SolicitudId", solicitudId.ToString(), TimeSpan.FromSeconds(5));

        var infoEmpleadoEsperada = new InformacionEmpleado(
            empleadoId, "CC", "999888777", "[TEST] Smoke ServiceBus", "[TEST] Verificacion");
        var infoEmpleadoPersistida = eventoPersistido
            .GetProperty("InformacionEmpleado").Deserialize<InformacionEmpleado>();
        infoEmpleadoPersistida.Should().Be(infoEmpleadoEsperada);

        var detalleTurnoEsperado = new DetalleTurno("[TEST] Turno Smoke SB", [
            new DetalleFranjaOrdinaria(
                new TimeOnly(8, 0), new TimeOnly(16, 0), 0,
                Array.Empty<DetalleSubFranja>(), Array.Empty<DetalleSubFranja>())
        ]);
        var detalleTurnoPersistido = eventoPersistido
            .GetProperty("DetalleTurno").Deserialize<DetalleTurno>();
        detalleTurnoPersistido.Should().BeEquivalentTo(detalleTurnoEsperado);
    }
}
