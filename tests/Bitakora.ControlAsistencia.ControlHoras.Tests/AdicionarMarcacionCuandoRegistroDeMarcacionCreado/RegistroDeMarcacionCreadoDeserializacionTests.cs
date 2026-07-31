// issue #270: guardrail de portabilidad por el bus para RegistroDeMarcacionCreado (reemplaza al
// guardrail de MarcacionRegistrada del issue #213: el evento que cruza el ASB interno del BC ahora
// es RegistroDeMarcacionCreado -- MarcacionRegistrada dejo de implementar IPrivateEvent, CA-3).

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AdicionarMarcacionCuandoRegistroDeMarcacionCreado;

/// <summary>
/// Verifica que RegistroDeMarcacionCreado (IPrivateEvent) sobrevive el cruce fisico del ASB interno
/// del BC (ADR-0024 decision #3): el productor lo serializa con Wolverine (camelCase) y el
/// consumidor lo deserializa con ServiceBusDeserializador, que usa el serializador por defecto
/// (PropertyNameCaseInsensitive, sin resolver custom). Este es el guardrail de portabilidad por el
/// bus (seccion 6e del test-writer) via el helper de produccion real -- distinto del guardrail
/// aislado en PrivateEvents.Tests (RegistroDeMarcacionCreadoTests, CA-2), que compila referenciando
/// unicamente PrivateEvents. El payload plano (string, DateTime, string?) debe reconstruirse sin
/// perdida via el constructor primario del record.
/// </summary>
public class RegistroDeMarcacionCreadoDeserializacionTests
{
    // JSON en formato camelCase - exactamente como Wolverine lo serializa al publicar al Service Bus.
    private const string JsonFormatoWolverine = """
        {
          "empleadoId": "EMP-001",
          "timestampNormalizado": "2026-03-15T08:09:00",
          "tipoMarcacion": "ENTRADA",
          "dispositivoId": "DEV-001"
        }
        """;

    // CA-5: el evento cruza el ASB y se reconstruye sin perdida via el serializador por defecto del bus.
    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoJsonEsFormatoWolverine()
    {
        var body = BinaryData.FromString(JsonFormatoWolverine);

        var evento = ServiceBusDeserializador.Deserializar<RegistroDeMarcacionCreado>(body);

        evento.Should().NotBeNull();
        evento.EmpleadoId.Should().Be("EMP-001");
        evento.TimestampNormalizado.Should().Be(new DateTime(2026, 3, 15, 8, 9, 0));
        evento.TipoMarcacion.Should().Be("ENTRADA");
        evento.DispositivoId.Should().Be("DEV-001");
    }

    // CA-5: los campos opcionales (nullable) que hacen al payload portable se preservan cuando estan ausentes.
    [Fact]
    public void Deserializar_PreservaCamposOpcionalesNulos_CuandoTipoYDispositivoAusentes()
    {
        var body = BinaryData.FromString("""
            {
              "empleadoId": "EMP-002",
              "timestampNormalizado": "2026-03-15T08:09:00"
            }
            """);

        var evento = ServiceBusDeserializador.Deserializar<RegistroDeMarcacionCreado>(body);

        evento.Should().NotBeNull();
        evento.EmpleadoId.Should().Be("EMP-002");
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
