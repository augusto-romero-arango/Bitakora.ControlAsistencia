using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.Programacion;

// Issue #861 CA-2: el evento cruza el bus; el destino deserializa SIN el resolver del productor.
public class ProgramacionTurnoDiarioSolicitadaPortabilidadTests
{
    private static readonly Guid SolicitudId = Guid.Parse("019600f0-0000-7000-8000-000000000861");
    private static readonly Guid JornadaId = Guid.Parse("019600f0-0000-7000-8000-000000000862");
    private static readonly ResumenColaborador Colaborador = new("CC-1234567890", "EMP-001", "Luis Augusto Barreto");
    private static readonly DateOnly Fecha = new(2026, 3, 15);

    private static readonly DetalleTurno Turno = new(
        "Turno Manana",
        new List<DetalleFranjaOrdinaria>
        {
            new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "(06:00-14:00)")
        }.AsReadOnly(),
        "Turno Manana (06:00-14:00)");

    private static JsonSerializerOptions CrearOpcionesBus() => new(JsonSerializerDefaults.Web);

    [Fact]
    public void RoundTrip_PreservaLaJornada_ConSerializadorPorDefectoDelBus()
    {
        var evento = new ProgramacionTurnoDiarioSolicitada(
            SolicitudId, Colaborador, Fecha, Turno, jornada: new DetalleJornada(JornadaId, 2520, 510, 240, 1));
        var opciones = CrearOpcionesBus();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<ProgramacionTurnoDiarioSolicitada>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Jornada.Should().Be(new DetalleJornada(JornadaId, 2520, 510, 240, 1));
        restaurado.SolicitudId.Should().Be(SolicitudId);
        restaurado.Fecha.Should().Be(Fecha);
    }

    [Fact]
    public void Deserializar_DejaJornadaEnNull_CuandoElMensajeViejoNoLlevaLaClave()
    {
        var opciones = CrearOpcionesBus();
        var evento = new ProgramacionTurnoDiarioSolicitada(SolicitudId, Colaborador, Fecha, Turno);
        var nodo = JsonNode.Parse(JsonSerializer.Serialize(evento, opciones))!.AsObject();
        nodo.Remove("jornada");

        var restaurado = JsonSerializer.Deserialize<ProgramacionTurnoDiarioSolicitada>(nodo.ToJsonString(), opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Jornada.Should().BeNull();
        restaurado.SolicitudId.Should().Be(SolicitudId);
    }
}
