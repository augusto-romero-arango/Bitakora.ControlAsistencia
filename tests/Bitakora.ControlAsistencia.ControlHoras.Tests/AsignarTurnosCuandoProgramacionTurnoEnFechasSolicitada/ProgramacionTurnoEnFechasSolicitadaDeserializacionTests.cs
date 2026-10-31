using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnosCuandoProgramacionTurnoEnFechasSolicitada;

public class ProgramacionTurnoEnFechasSolicitadaDeserializacionTests
{
    private static readonly Guid SolicitudId = Guid.Parse("019600b0-0000-7000-8000-000000000001");
    private static readonly Guid JornadaId = Guid.Parse("019600b0-0000-7000-8000-0000000000aa");

    private const string JsonFormatoWolverine = """
        {
          "solicitudId": "019600b0-0000-7000-8000-000000000001",
          "colaborador": {
            "identificacion": "CC-1234567890",
            "codigoColaborador": "EMP-001",
            "nombreCompleto": "Luis Augusto Barreto"
          },
          "fechas": ["2026-03-15", "2026-03-16"],
          "detalleTurno": {
            "nombre": "Turno Manana",
            "franjasOrdinarias": [
              {
                "horaInicio": "08:00:00",
                "horaFin": "16:00:00",
                "diaOffsetFin": 0,
                "descansos": [],
                "extras": []
              }
            ]
          },
          "jornada": {
            "jornadaId": "019600b0-0000-7000-8000-0000000000aa",
            "horasSemanalesEnMinutos": 2880,
            "topeDiarioEnMinutos": 600,
            "minimoDiarioEnMinutos": 0,
            "diasDescansoPorSemana": 1
          }
        }
        """;

    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoJsonEsFormatoWolverine()
    {
        var body = BinaryData.FromString(JsonFormatoWolverine);

        var evento = ServiceBusDeserializador.Deserializar<ProgramacionTurnoEnFechasSolicitada>(body);

        evento.Should().NotBeNull();
        evento.SolicitudId.Should().Be(SolicitudId);
        evento.Colaborador.Should().NotBeNull();
        evento.Colaborador.Identificacion.Should().Be("CC-1234567890");
        evento.Colaborador.CodigoColaborador.Should().Be("EMP-001");
        evento.Colaborador.NombreCompleto.Should().Be("Luis Augusto Barreto");
        evento.Fechas.Should().Equal(new DateOnly(2026, 3, 15), new DateOnly(2026, 3, 16));
        evento.DetalleTurno.Should().NotBeNull();
        evento.DetalleTurno.Nombre.Should().Be("Turno Manana");
        evento.DetalleTurno.FranjasOrdinarias.Should().HaveCount(1);
        evento.DetalleTurno.FranjasOrdinarias[0].HoraInicio.Should().Be(new TimeOnly(8, 0));
        evento.DetalleTurno.FranjasOrdinarias[0].HoraFin.Should().Be(new TimeOnly(16, 0));
        evento.Jornada.JornadaId.Should().Be(JornadaId);
        evento.Jornada.TopeDiarioEnMinutos.Should().Be(600);
    }

    [Fact]
    public void Deserializar_LanzaExcepcion_CuandoJsonEsInvalido()
    {
        var body = BinaryData.FromString("esto no es json");

        var act = () => ServiceBusDeserializador.Deserializar<ProgramacionTurnoEnFechasSolicitada>(body);

        act.Should().ThrowExactly<JsonException>();
    }
}
