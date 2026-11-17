using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.Programacion;

public class ProgramacionTurnoEnFechasSolicitadaPortabilidadTests
{
    private static readonly Guid SolicitudId = Guid.Parse("019600f0-0000-7000-8000-000000001337");
    private static readonly ResumenColaborador Colaborador = new("CC-1234567890", "EMP-001", "Luis Augusto Barreto");
    private static readonly DateOnly Fecha1 = new(2026, 3, 15);
    private static readonly DateOnly Fecha2 = new(2026, 3, 16);

    private static readonly DetalleTurno Turno = new(
        "Turno Partido",
        new List<DetalleFranjaOrdinaria>
        {
            new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
                [new DetalleSubFranja(new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0, "10:00-10:15")],
                [new DetalleSubFranja(new TimeOnly(13, 0), new TimeOnly(14, 0), 0, 0, "13:00-14:00")],
                "06:00-14:00, descanso 10:00-10:15, extra 13:00-14:00")
        }.AsReadOnly(),
        "06:00-14:00, descanso 10:00-10:15, extra 13:00-14:00");

    private static JsonSerializerOptions CrearOpcionesBus() => new(JsonSerializerDefaults.Web);

    [Fact]
    public void RoundTrip_PreservaTodosLosCampos_ConSerializadorPorDefectoDelBus()
    {
        var sede = new DetalleSede("SEDE-01", "Sede Principal");
        var evento = new ProgramacionTurnoEnFechasSolicitada(
            SolicitudId, Colaborador, [Fecha1, Fecha2], Turno, sede);
        var opciones = CrearOpcionesBus();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<ProgramacionTurnoEnFechasSolicitada>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.SolicitudId.Should().Be(SolicitudId);
        restaurado.Colaborador.Should().Be(Colaborador);
        restaurado.Fechas.Should().Equal(Fecha1, Fecha2);
        restaurado.DetalleTurno.Should().Be(Turno);
        restaurado.Sede.Should().Be(sede);
    }

    [Fact]
    public void RoundTrip_ConservaLaSedeNula_ConSerializadorPorDefectoDelBus()
    {
        var evento = new ProgramacionTurnoEnFechasSolicitada(
            SolicitudId, Colaborador, [Fecha1], Turno);
        var opciones = CrearOpcionesBus();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<ProgramacionTurnoEnFechasSolicitada>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Sede.Should().BeNull();
        restaurado.Fechas.Should().Equal(Fecha1);
    }

    // Un mensaje publicado antes del retiro de la Jornada (#1520) puede seguir en vuelo durante el
    // despliegue: STJ ignora por defecto la propiedad que el tipo ya no declara.
    [Fact]
    public void Deserializar_IgnoraLaJornada_CuandoElMensajeEnVueloLaTrae()
    {
        var json = """
            {"solicitudId":"019600f0-0000-7000-8000-000000001337",
             "colaborador":{"identificacion":"CC-1234567890","codigoColaborador":"EMP-001","nombreCompleto":"Luis Augusto Barreto"},
             "fechas":["2026-03-15"],
             "detalleTurno":{"nombre":"Turno Partido","franjasOrdinarias":[],"descripcion":"06:00-14:00"},
             "jornada":{"jornadaId":"019600f0-0000-7000-8000-000000001338","horasSemanalesEnMinutos":2520,
                        "topeDiarioEnMinutos":510,"minimoDiarioEnMinutos":240,"diasDescansoPorSemana":1},
             "sede":null}
            """;

        var restaurado = JsonSerializer.Deserialize<ProgramacionTurnoEnFechasSolicitada>(json, CrearOpcionesBus());

        restaurado.Should().NotBeNull();
        restaurado!.SolicitudId.Should().Be(SolicitudId);
        restaurado.Colaborador.Should().Be(Colaborador);
        restaurado.Fechas.Should().Equal(Fecha1);
        restaurado.DetalleTurno.Nombre.Should().Be("Turno Partido");
        restaurado.Sede.Should().BeNull();
    }
}
