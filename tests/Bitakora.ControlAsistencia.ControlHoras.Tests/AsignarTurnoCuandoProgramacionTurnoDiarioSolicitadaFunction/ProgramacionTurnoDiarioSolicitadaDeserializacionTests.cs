// HU-29: Corregir deserializacion de mensajes Service Bus en endpoints consumidores

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction;

/// <summary>
/// Verifica que ProgramacionTurnoDiarioSolicitada se deserializa correctamente
/// desde el formato que produce Wolverine al publicar al Service Bus (camelCase JSON).
/// Sin PropertyNameCaseInsensitive=true, todas las propiedades quedan null
/// provocando NullReferenceException en el handler (linea: command.Colaborador.CodigoColaborador).
/// </summary>
public class ProgramacionTurnoDiarioSolicitadaDeserializacionTests
{
    private static readonly Guid SolicitudId =
        Guid.Parse("019600b0-0000-7000-8000-000000000001");

    // JSON en formato camelCase - exactamente como Wolverine lo serializa al publicar al Service Bus.
    // Wolverine usa System.Text.Json con camelCase naming policy por defecto.
    private const string JsonFormatoWolverine = """
        {
          "solicitudId": "019600b0-0000-7000-8000-000000000001",
          "colaborador": {
            "identificacion": "CC-1234567890",
            "codigoColaborador": "EMP-001",
            "nombreCompleto": "Luis Augusto Barreto"
          },
          "fecha": "2026-03-15",
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
          }
        }
        """;

    // CA: Los mensajes ProgramacionTurnoDiarioSolicitada se deserializan correctamente en ControlHoras
    // CA: Existe un test que verifica que la deserializacion funciona con el formato que produce Wolverine
    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoJsonEsFormatoWolverine()
    {
        var body = BinaryData.FromString(JsonFormatoWolverine);

        var evento = ServiceBusDeserializador.Deserializar<ProgramacionTurnoDiarioSolicitada>(body);

        evento.Should().NotBeNull();
        evento.SolicitudId.Should().Be(SolicitudId);
        evento.Colaborador.Should().NotBeNull();
        evento.Colaborador.Identificacion.Should().Be("CC-1234567890");
        evento.Colaborador.CodigoColaborador.Should().Be("EMP-001");
        evento.Colaborador.NombreCompleto.Should().Be("Luis Augusto Barreto");
        evento.Fecha.Should().Be(new DateOnly(2026, 3, 15));
        evento.DetalleTurno.Should().NotBeNull();
        evento.DetalleTurno.Nombre.Should().Be("Turno Manana");
        evento.DetalleTurno.FranjasOrdinarias.Should().HaveCount(1);
        evento.DetalleTurno.FranjasOrdinarias[0].HoraInicio.Should().Be(new TimeOnly(8, 0));
        evento.DetalleTurno.FranjasOrdinarias[0].HoraFin.Should().Be(new TimeOnly(16, 0));
    }

    [Fact]
    public void Deserializar_LanzaExcepcion_CuandoJsonEsInvalido()
    {
        var body = BinaryData.FromString("esto no es json");

        var act = () => ServiceBusDeserializador.Deserializar<ProgramacionTurnoDiarioSolicitada>(body);

        act.Should().ThrowExactly<JsonException>();
    }
}
