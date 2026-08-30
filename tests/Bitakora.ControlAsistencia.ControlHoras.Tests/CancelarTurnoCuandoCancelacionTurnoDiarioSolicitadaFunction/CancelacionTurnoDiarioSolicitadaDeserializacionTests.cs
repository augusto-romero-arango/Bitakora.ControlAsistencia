// Issue #499 CA-5: el payload de CancelacionTurnoDiarioSolicitada (evento nacido en #498) debe
// deserializar desde el bus con el serializador por defecto del consumidor -- mismo camino que
// ProgramacionTurnoDiarioSolicitadaDeserializacionTests para el evento hermano de asignacion.

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.CancelarTurnoCuandoCancelacionTurnoDiarioSolicitadaFunction;

public class CancelacionTurnoDiarioSolicitadaDeserializacionTests
{
    private static readonly Guid SolicitudId =
        Guid.Parse("019600b0-0000-7000-8000-000000000010");

    // JSON en formato camelCase - exactamente como Wolverine lo serializa al publicar al Service Bus.
    private const string JsonFormatoWolverine = """
        {
          "solicitudId": "019600b0-0000-7000-8000-000000000010",
          "colaborador": {
            "identificacion": "CC-1234567890",
            "codigoColaborador": "EMP-001",
            "nombreCompleto": "Luis Augusto Barreto"
          },
          "fecha": "2026-03-15"
        }
        """;

    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoJsonEsFormatoWolverine()
    {
        var body = BinaryData.FromString(JsonFormatoWolverine);

        var evento = ServiceBusDeserializador.Deserializar<CancelacionTurnoDiarioSolicitada>(body);

        evento.Should().NotBeNull();
        evento.SolicitudId.Should().Be(SolicitudId);
        evento.Colaborador.Should().NotBeNull();
        evento.Colaborador.Identificacion.Should().Be("CC-1234567890");
        evento.Colaborador.CodigoColaborador.Should().Be("EMP-001");
        evento.Colaborador.NombreCompleto.Should().Be("Luis Augusto Barreto");
        evento.Fecha.Should().Be(new DateOnly(2026, 3, 15));
    }

    [Fact]
    public void Deserializar_LanzaExcepcion_CuandoJsonEsInvalido()
    {
        var body = BinaryData.FromString("esto no es json");

        var act = () => ServiceBusDeserializador.Deserializar<CancelacionTurnoDiarioSolicitada>(body);

        act.Should().ThrowExactly<JsonException>();
    }
}
