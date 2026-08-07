// Issue #288 CA-7: ProgramacionTurnoDiarioSolicitada es IPrivateEvent (cruza el ASB interno del BC,
// ADR-0024 decision #3). Descripcion (agregada a DetalleTurno/DetalleFranjaOrdinaria/DetalleSubFranja)
// es un string plano: sigue cumpliendo CA-ADR-0025 ("lo que cruza un bus es plano"). Este test
// verifica portabilidad con el serializador POR DEFECTO (sin el resolver custom de Programacion,
// que es quien produce el evento) -- exactamente lo que ve ControlHoras al consumirlo del bus.
// Distinto del round-trip de Marten (6d): aqui la expectativa es que Descripcion SOBREVIVA sin
// resolver, porque el DTO plano no necesita uno (a diferencia de los tipos ricos con campos
// privados como SubFranja/FranjaOrdinaria).

using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;

public class ProgramacionTurnoDiarioSolicitadaPortabilidadTests
{
    private static readonly Guid SolicitudId =
        Guid.Parse("019600b0-0000-7000-8000-000000000009");

    // Issue #318 CA-2: Empleado ahora tipa con DetalleEmpleado (payload propio de PrivateEvents).
    private static readonly DetalleEmpleado Empleado = new(
        "EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    private static readonly DateOnly Fecha = new(2026, 3, 15);

    // Serializador del productor (Programacion): sin resolver custom, DetalleTurno es un DTO plano
    // (solo tipos primitivos y listas), no necesita uno.
    private static JsonSerializerOptions CrearOpcionesProductor() =>
        new(JsonSerializerDefaults.Web);

    // Turno con descanso: cubre los tres niveles de Descripcion (turno, franja y sub-franja).
    private static DetalleTurno CrearDetalleTurno() => new(
        "Turno Manana",
        [new DetalleFranjaOrdinaria(
            new TimeOnly(8, 0), new TimeOnly(16, 0), 0,
            [new DetalleSubFranja(new TimeOnly(12, 0), new TimeOnly(13, 0), 0, 0, "(12:00-13:00)")],
            [],
            "(08:00-16:00)[Descansos:(12:00-13:00)]")],
        "Turno Manana (08:00-16:00)[Descansos:(12:00-13:00)]");

    [Fact]
    public void RoundTrip_PreservaDescripcion_ConSerializadorPorDefectoDelBus()
    {
        var detalleTurno = CrearDetalleTurno();
        var evento = new ProgramacionTurnoDiarioSolicitada(SolicitudId, Empleado, Fecha, detalleTurno);

        // El productor publica con sus propias opciones (sin resolver custom, DTO plano)...
        var json = JsonSerializer.Serialize(evento, CrearOpcionesProductor());

        // ...y el consumidor (ControlHoras) deserializa con SUS opciones (case-insensitive,
        // tampoco con resolver custom) -- el mismo camino que ServiceBusDeserializador usa en produccion.
        var body = BinaryData.FromString(json);
        var restaurado = ServiceBusDeserializador.Deserializar<ProgramacionTurnoDiarioSolicitada>(body);

        restaurado.Should().NotBeNull();
        var franjaRestaurada = restaurado.DetalleTurno.FranjasOrdinarias[0];
        restaurado.DetalleTurno.Descripcion.Should().Be(detalleTurno.Descripcion);
        franjaRestaurada.Descripcion.Should().Be(detalleTurno.FranjasOrdinarias[0].Descripcion);
        franjaRestaurada.Descansos[0].Descripcion
            .Should().Be(detalleTurno.FranjasOrdinarias[0].Descansos[0].Descripcion);
    }

    // Issue #331 CA-5: la sede es un DTO plano de strings (DetalleSede) -- portable por el
    // serializador por defecto del bus, igual que DetalleTurno/DetalleEmpleado.
    [Fact]
    public void RoundTrip_PreservaLaSede_ConSerializadorPorDefectoDelBus()
    {
        var detalleTurno = CrearDetalleTurno();
        var sede = new DetalleSede("SEDE-01", "Sede Principal");
        var evento = new ProgramacionTurnoDiarioSolicitada(SolicitudId, Empleado, Fecha, detalleTurno, sede);

        var json = JsonSerializer.Serialize(evento, CrearOpcionesProductor());

        var body = BinaryData.FromString(json);
        var restaurado = ServiceBusDeserializador.Deserializar<ProgramacionTurnoDiarioSolicitada>(body);

        restaurado.Should().NotBeNull();
        restaurado.Sede.Should().Be(sede);
    }

    // Issue #331 CA-2/CA-4: sede es opcional y aditiva -- los mensajes publicados antes de este
    // issue no llevan la clave "sede" en el JSON del bus. Serializar el evento con Sede = null y
    // volver a leerlo solo probaria que un null explicito ("sede": null) sobrevive; lo que este
    // test ejercita es la AUSENCIA de la clave, que es la forma real de los mensajes viejos --
    // el mismo camino que ya cubre Deserializar_DejaDescripcionEnCadenaVacia_... para #288.
    [Fact]
    public void Deserializar_DejaSedeEnNull_CuandoElMensajeNoLlevaLaClaveSede()
    {
        var restaurado = ServiceBusDeserializador.Deserializar<ProgramacionTurnoDiarioSolicitada>(
            BinaryData.FromString(JsonSinLaClaveSede()));

        restaurado.Should().NotBeNull();
        restaurado.Sede.Should().BeNull();
        restaurado.DetalleTurno.Nombre.Should().Be("Turno Manana");
    }

    // La forma anterior a #331 es exactamente la actual SIN la clave "sede" -- quitarla del JSON
    // canonico expresa esa relacion mejor que reescribir el mensaje completo a mano (mismo recurso
    // que JsonConFormaPersistidaSinDescripcion en Programacion.Tests). Se parte de un evento CON
    // sede para que la asercion sobre Remove() delate un test vacuo si la clave cambiara de nombre.
    private static string JsonSinLaClaveSede()
    {
        var conSede = new ProgramacionTurnoDiarioSolicitada(
            SolicitudId, Empleado, Fecha, CrearDetalleTurno(), new DetalleSede("SEDE-01", "Sede Principal"));

        var nodo = JsonNode.Parse(JsonSerializer.Serialize(conSede, CrearOpcionesProductor()))!;
        nodo.AsObject().Remove("sede").Should().BeTrue(
            "el JSON del bus debe llevar la clave 'sede' para que quitarla represente la forma anterior a #331");

        return nodo.ToJsonString();
    }

    // Retro-compatibilidad: los eventos publicados antes de #288 no llevan el campo. STJ dejaria
    // null en un parametro posicional declarado como string no anulable, lo que seria una mina
    // para cualquier consumidor. Los DTOs lo normalizan a cadena vacia -- la consecuencia que el
    // issue asumio explicitamente ("habra mezcla entre solicitudes viejas sin texto y nuevas").
    [Fact]
    public void Deserializar_DejaDescripcionEnCadenaVacia_CuandoEventoNoLlevaElCampo()
    {
        const string jsonPrevioAlCampo = """
            {
              "solicitudId": "019600b0-0000-7000-8000-000000000009",
              "informacionEmpleado": {
                "empleadoId": "EMP-001", "tipoDocumento": "CC", "numeroDocumento": "1234567890",
                "nombres": "Luis Augusto", "apellidos": "Barreto"
              },
              "fecha": "2026-03-15",
              "detalleTurno": {
                "nombre": "Turno Manana",
                "franjasOrdinarias": [{
                  "horaInicio": "08:00:00", "horaFin": "16:00:00", "diaOffsetFin": 0,
                  "descansos": [{
                    "horaInicio": "12:00:00", "horaFin": "13:00:00",
                    "diaOffsetInicio": 0, "diaOffsetFin": 0
                  }],
                  "extras": []
                }]
              }
            }
            """;

        var restaurado = ServiceBusDeserializador.Deserializar<ProgramacionTurnoDiarioSolicitada>(
            BinaryData.FromString(jsonPrevioAlCampo));

        restaurado.Should().NotBeNull();
        var franja = restaurado.DetalleTurno.FranjasOrdinarias[0];
        restaurado.DetalleTurno.Descripcion.Should().BeEmpty();
        franja.Descripcion.Should().BeEmpty();
        franja.Descansos[0].Descripcion.Should().BeEmpty();
    }
}
