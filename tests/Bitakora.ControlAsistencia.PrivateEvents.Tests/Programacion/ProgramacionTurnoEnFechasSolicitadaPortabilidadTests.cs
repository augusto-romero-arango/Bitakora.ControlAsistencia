using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.Programacion;

public class ProgramacionTurnoEnFechasSolicitadaPortabilidadTests
{
    private static readonly Guid SolicitudId = Guid.Parse("019600f0-0000-7000-8000-000000001337");
    private static readonly Guid JornadaId = Guid.Parse("019600f0-0000-7000-8000-000000001338");
    private static readonly ResumenColaborador Colaborador = new("CC-1234567890", "EMP-001", "Luis Augusto Barreto");
    private static readonly DateOnly Fecha1 = new(2026, 3, 15);
    private static readonly DateOnly Fecha2 = new(2026, 3, 16);
    private static readonly DetalleJornada Jornada = new(JornadaId, 2520, 510, 240, 1);

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
            SolicitudId, Colaborador, [Fecha1, Fecha2], Turno, Jornada, sede);
        var opciones = CrearOpcionesBus();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<ProgramacionTurnoEnFechasSolicitada>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.SolicitudId.Should().Be(SolicitudId);
        restaurado.Colaborador.Should().Be(Colaborador);
        restaurado.Fechas.Should().Equal(Fecha1, Fecha2);
        restaurado.DetalleTurno.Should().Be(Turno);
        restaurado.Jornada.Should().Be(Jornada);
        restaurado.Sede.Should().Be(sede);
    }

    [Fact]
    public void RoundTrip_ConservaLaSedeNula_ConSerializadorPorDefectoDelBus()
    {
        var evento = new ProgramacionTurnoEnFechasSolicitada(
            SolicitudId, Colaborador, [Fecha1], Turno, Jornada);
        var opciones = CrearOpcionesBus();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<ProgramacionTurnoEnFechasSolicitada>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Sede.Should().BeNull();
        restaurado.Fechas.Should().Equal(Fecha1);
    }

    [Fact]
    public void RoundTrip_PreservaLaEstampaDeLaJornada_ConSerializadorPorDefectoDelBus()
    {
        var jornada = new DetalleJornada(JornadaId, 2520, 510, 240, 1, "Flexible", true, 540, 240, 2520);
        var evento = new ProgramacionTurnoEnFechasSolicitada(SolicitudId, Colaborador, [Fecha1], Turno, jornada);
        var opciones = CrearOpcionesBus();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<ProgramacionTurnoEnFechasSolicitada>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Jornada.Should().Be(jornada);
        restaurado.Jornada!.Modalidad.Should().Be("Flexible");
        restaurado.Jornada.TopeDiarioDeLeyEnMinutos.Should().Be(540);
        restaurado.Jornada.MinimoDiarioDeLeyEnMinutos.Should().Be(240);
        restaurado.Jornada.HorasSemanalesDeLeyEnMinutos.Should().Be(2520);
    }

    [Fact]
    public void Deserializar_DejaModalidadNulaMarcaFalsaYLeyNula_CuandoElMensajeEnVueloNoTraeLosCamposNuevos()
    {
        var json = """
            {"jornadaId":"019600f0-0000-7000-8000-000000001338","horasSemanalesEnMinutos":2520,
             "topeDiarioEnMinutos":510,"minimoDiarioEnMinutos":240,"diasDescansoPorSemana":1}
            """;

        var restaurado = JsonSerializer.Deserialize<DetalleJornada>(json, CrearOpcionesBus());

        restaurado.Should().Be(new DetalleJornada(JornadaId, 2520, 510, 240, 1));
        restaurado!.Modalidad.Should().BeNull();
        restaurado.DescansoAcumulado.Should().BeFalse();
        restaurado.TopeDiarioDeLeyEnMinutos.Should().BeNull();
        restaurado.MinimoDiarioDeLeyEnMinutos.Should().BeNull();
        restaurado.HorasSemanalesDeLeyEnMinutos.Should().BeNull();
    }
}
