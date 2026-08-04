// Issue #288 CA-7: ProgramacionTurnoDiarioSolicitada es IPrivateEvent (cruza el ASB interno del BC,
// ADR-0024 decision #3). Descripcion (agregada a DetalleTurno/DetalleFranjaOrdinaria/DetalleSubFranja)
// es un string plano: sigue cumpliendo CA-ADR-0025 ("lo que cruza un bus es plano"). Este test
// verifica portabilidad con el serializador POR DEFECTO (sin el resolver custom de Programacion,
// que es quien produce el evento) -- exactamente lo que ve ControlHoras al consumirlo del bus.
// Distinto del round-trip de Marten (6d): aqui la expectativa es que Descripcion SOBREVIVA sin
// resolver, porque el DTO plano no necesita uno (a diferencia de los tipos ricos con campos
// privados como SubFranja/FranjaOrdinaria).

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.PublicEvents.Empleados;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;

public class ProgramacionTurnoDiarioSolicitadaPortabilidadTests
{
    private static readonly Guid SolicitudId =
        Guid.Parse("019600b0-0000-7000-8000-000000000009");

    private static readonly InformacionEmpleado Empleado = new(
        "EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    private static readonly DateOnly Fecha = new(2026, 3, 15);

    // Serializador del productor (Programacion): sin resolver custom, DetalleTurno es un DTO plano
    // (solo tipos primitivos y listas), no necesita uno.
    private static JsonSerializerOptions CrearOpcionesProductor() =>
        new(JsonSerializerDefaults.Web);

    [Fact]
    public void RoundTrip_PreservaDescripcion_ConSerializadorPorDefectoDelBus()
    {
        var detalleTurno = new DetalleTurno(
            "Turno Manana",
            [new DetalleFranjaOrdinaria(
                new TimeOnly(8, 0), new TimeOnly(16, 0), 0, [], [],
                "(08:00-16:00)")],
            "Turno Manana (08:00-16:00)");
        var evento = new ProgramacionTurnoDiarioSolicitada(SolicitudId, Empleado, Fecha, detalleTurno);

        // El productor publica con sus propias opciones (sin resolver custom, DTO plano)...
        var json = JsonSerializer.Serialize(evento, CrearOpcionesProductor());

        // ...y el consumidor (ControlHoras) deserializa con SUS opciones (case-insensitive,
        // tampoco con resolver custom) -- el mismo camino que ServiceBusDeserializador usa en produccion.
        var body = BinaryData.FromString(json);
        var restaurado = ServiceBusDeserializador.Deserializar<ProgramacionTurnoDiarioSolicitada>(body);

        restaurado.Should().NotBeNull();
        restaurado.DetalleTurno.Descripcion.Should().Be(detalleTurno.Descripcion);
        restaurado.DetalleTurno.FranjasOrdinarias[0].Descripcion
            .Should().Be(detalleTurno.FranjasOrdinarias[0].Descripcion);
    }
}
