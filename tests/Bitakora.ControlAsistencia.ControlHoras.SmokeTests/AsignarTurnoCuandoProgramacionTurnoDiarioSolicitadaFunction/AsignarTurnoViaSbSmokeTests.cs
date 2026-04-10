using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.Empleados.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;
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
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

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

    /// <summary>
    /// Regression test para el bug del issue #29.
    /// Wolverine serializa con camelCase. El endpoint consumidor usaba ToObjectFromJson
    /// con opciones default (case-sensitive), lo que causaba que todas las propiedades
    /// quedaran null y se lanzara NullReferenceException.
    /// Este test verifica que ServiceBusDeserializador con PropertyNameCaseInsensitive=true
    /// resuelve el problema en produccion.
    /// </summary>
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeAsignarTurnoDiario_CuandoMensajeTieneFormatoCamelCaseDeWolverine()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        // Arrange: mensaje en camelCase, exactamente como lo serializa Wolverine
        var correlationId = Guid.CreateVersion7().ToString();
        var solicitudId = Guid.CreateVersion7();
        var empleadoId = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 4, 10);

        // Propiedades en camelCase simulan la serializacion real de Wolverine.
        // Antes del fix este formato causaba NullReferenceException en el handler
        // porque ToObjectFromJson usa case-sensitive por defecto.
        var eventoEnFormatoWolverine = new
        {
            solicitudId = solicitudId,
            empleado = new
            {
                empleadoId = empleadoId,
                tipoIdentificacion = "CC",
                numeroIdentificacion = "111222333",
                nombres = "[TEST] Smoke Wolverine",
                apellidos = "[TEST] CamelCase Fix"
            },
            fecha = fecha.ToString("yyyy-MM-dd"),
            detalleTurno = new
            {
                nombre = "[TEST] Turno Wolverine CamelCase",
                franjasOrdinarias = new[]
                {
                    new
                    {
                        horaInicio = "07:00:00",
                        horaFin = "15:00:00",
                        diaOffsetFin = 0,
                        descansos = Array.Empty<object>(),
                        extras = Array.Empty<object>()
                    }
                }
            }
        };

        // Act: publicar al topic en formato camelCase
        await serviceBus.PublishAsync(TopicEntrada, eventoEnFormatoWolverine, correlationId);

        // Assert: verificar persistencia en Postgres.
        // Si la deserializacion falla (propiedades null), el handler lanza
        // NullReferenceException, el mensaje va a dead-letter y NUNCA se persiste.
        var streamId = $"{empleadoId}:{fecha:yyyy-MM-dd}";
        var tipoEvento = "turno_diario_asignado";

        var existe = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, tipoEvento, Timeout,
            campoJson: "SolicitudId", valorJson: solicitudId.ToString());

        existe.Should().BeTrue(
            $"el evento {tipoEvento} con SolicitudId {solicitudId} deberia existir. " +
            $"Si falla, ServiceBusDeserializador no esta usando PropertyNameCaseInsensitive=true.");

        // Assert detallado: verificar que los datos se mapearon correctamente
        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaControlHoras, streamId, tipoEvento,
            "SolicitudId", solicitudId.ToString(), TimeSpan.FromSeconds(5));

        var infoEmpleadoEsperada = new InformacionEmpleado(
            empleadoId, "CC", "111222333", "[TEST] Smoke Wolverine", "[TEST] CamelCase Fix");
        var infoEmpleadoPersistida = eventoPersistido
            .GetProperty("InformacionEmpleado").Deserialize<InformacionEmpleado>();
        infoEmpleadoPersistida.Should().Be(infoEmpleadoEsperada);

        var detalleTurnoEsperado = new DetalleTurno("[TEST] Turno Wolverine CamelCase", [
            new DetalleFranjaOrdinaria(
                new TimeOnly(7, 0), new TimeOnly(15, 0), 0,
                Array.Empty<DetalleSubFranja>(), Array.Empty<DetalleSubFranja>())
        ]);
        var detalleTurnoPersistido = eventoPersistido
            .GetProperty("DetalleTurno").Deserialize<DetalleTurno>();
        detalleTurnoPersistido.Should().BeEquivalentTo(detalleTurnoEsperado);
    }
}
