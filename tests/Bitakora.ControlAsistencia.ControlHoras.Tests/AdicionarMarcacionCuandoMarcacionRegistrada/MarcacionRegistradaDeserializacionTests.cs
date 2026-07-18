// issue #213: guardrail de portabilidad por el bus para MarcacionRegistrada.

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction.Eventos;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AdicionarMarcacionCuandoMarcacionRegistrada;

/// <summary>
/// Verifica que MarcacionRegistrada (IPrivateEvent) sobrevive el cruce fisico del ASB interno del BC
/// (issue #213 / ADR-0024 decision #3): el productor la serializa con Wolverine (camelCase) y el
/// consumidor la deserializa con ServiceBusDeserializador, que usa el serializador por defecto
/// (PropertyNameCaseInsensitive, SIN el resolver custom de Marten). Este es el guardrail de
/// portabilidad por el bus: distinto de MarcacionRegistradaSerializacionTests, que ejercita el
/// round-trip del event store (con CrearOpcionesMarten y el resolver del dominio) y pasaria incluso
/// si el payload no fuera portable. El payload plano de MarcacionRegistrada (string, DateTime,
/// string?) debe reconstruirse sin perdida via su constructor publico.
/// </summary>
public class MarcacionRegistradaDeserializacionTests
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

    // CA-2: el evento cruza el ASB y se reconstruye sin perdida via el serializador por defecto del bus.
    [Fact]
    public void Deserializar_ReconstruyeEvento_CuandoJsonEsFormatoWolverine()
    {
        var body = BinaryData.FromString(JsonFormatoWolverine);

        var evento = ServiceBusDeserializador.Deserializar<MarcacionRegistrada>(body);

        evento.Should().NotBeNull();
        evento.EmpleadoId.Should().Be("EMP-001");
        evento.TimestampNormalizado.Should().Be(new DateTime(2026, 3, 15, 8, 9, 0));
        evento.TipoMarcacion.Should().Be("ENTRADA");
        evento.DispositivoId.Should().Be("DEV-001");
    }

    // CA-2: los campos opcionales (nullable) que hacen al payload portable se preservan cuando estan ausentes.
    [Fact]
    public void Deserializar_PreservaCamposOpcionalesNulos_CuandoTipoYDispositivoAusentes()
    {
        var body = BinaryData.FromString("""
            {
              "empleadoId": "EMP-002",
              "timestampNormalizado": "2026-03-15T08:09:00"
            }
            """);

        var evento = ServiceBusDeserializador.Deserializar<MarcacionRegistrada>(body);

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

        var act = () => ServiceBusDeserializador.Deserializar<MarcacionRegistrada>(body);

        act.Should().ThrowExactly<JsonException>();
    }
}
