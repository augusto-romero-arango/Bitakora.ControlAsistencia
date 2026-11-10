using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AdicionarMarcacionCuandoRegistroDeMarcacionCreado;

/// <summary>
/// El productor serializa con Wolverine (camelCase) y el consumidor deserializa con
/// ServiceBusDeserializador (serializador por defecto, case-insensitive, sin resolver custom).
/// </summary>
public class RegistroDeMarcacionCreadoDeserializacionTests
{
    // Exactamente como Wolverine lo serializa al publicar al Service Bus.
    private const string JsonFormatoWolverine = """
        {
          "codigoColaborador": "EMP-001",
          "timestampNormalizado": "2026-03-15T08:09:00",
          "tipoMarcacion": "ENTRADA",
          "dispositivoId": "DEV-001"
        }
        """;

    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoJsonEsFormatoWolverine()
    {
        var body = BinaryData.FromString(JsonFormatoWolverine);

        var evento = ServiceBusDeserializador.Deserializar<RegistroDeMarcacionCreado>(body);

        evento.Should().NotBeNull();
        evento.CodigoColaborador.Should().Be("EMP-001");
        evento.TimestampNormalizado.Should().Be(new DateTime(2026, 3, 15, 8, 9, 0));
        evento.TipoMarcacion.Should().Be("ENTRADA");
        evento.DispositivoId.Should().Be("DEV-001");
    }

    [Fact]
    public void Deserializar_PreservaCamposOpcionalesNulos_CuandoTipoYDispositivoAusentes()
    {
        var body = BinaryData.FromString("""
            {
              "codigoColaborador": "EMP-002",
              "timestampNormalizado": "2026-03-15T08:09:00"
            }
            """);

        var evento = ServiceBusDeserializador.Deserializar<RegistroDeMarcacionCreado>(body);

        evento.Should().NotBeNull();
        evento.CodigoColaborador.Should().Be("EMP-002");
        evento.TimestampNormalizado.Should().Be(new DateTime(2026, 3, 15, 8, 9, 0));
        evento.TipoMarcacion.Should().BeNull();
        evento.DispositivoId.Should().BeNull();
    }

    [Fact]
    public void Deserializar_LanzaExcepcion_CuandoJsonEsInvalido()
    {
        var body = BinaryData.FromString("esto no es json");

        var act = () => ServiceBusDeserializador.Deserializar<RegistroDeMarcacionCreado>(body);

        act.Should().ThrowExactly<JsonException>();
    }
}
