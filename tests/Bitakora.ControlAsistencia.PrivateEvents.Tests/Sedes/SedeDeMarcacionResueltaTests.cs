using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Sedes;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.Sedes;

/// <summary>
/// Guardrail de portabilidad por el bus: el productor (Wolverine) serializa en camelCase y el
/// consumidor deserializa SIN resolver custom. Al reves que un round-trip de Marten, aqui la
/// expectativa es que el payload sobreviva sin resolver -- si no lo hiciera, el tipo no seria
/// portable (MEF-ADR-0023).
///
/// Compila referenciando UNICAMENTE PrivateEvents (CA-ADR-0029 seccion 3): si necesitara mas, el
/// tipo no seria portable.
/// </summary>
public class SedeDeMarcacionResueltaTests
{
    // Simula al productor: Wolverine serializa en camelCase por defecto al publicar al Service Bus.
    private static JsonSerializerOptions CrearOpcionesProductor() =>
        new(JsonSerializerDefaults.Web);

    // Simula al consumidor: deserializacion por defecto, sin resolver custom.
    private static JsonSerializerOptions CrearOpcionesConsumidor() =>
        new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void RoundTrip_ReconstruyeEvento_CuandoElPayloadEstaCompleto()
    {
        var evento = new SedeDeMarcacionResuelta(
            "EMP-001",
            new DateTime(2026, 3, 15, 8, 9, 0),
            "DEV-001",
            "001",
            "Sede Principal",
            "CC-100");

        var json = JsonSerializer.Serialize(evento, CrearOpcionesProductor());
        var restaurado = JsonSerializer.Deserialize<SedeDeMarcacionResuelta>(json, CrearOpcionesConsumidor());

        // La igualdad por valor del record cubre los seis campos: payload plano, sin coleccion ni
        // VO anidado.
        restaurado.Should().Be(evento);
    }

    [Fact]
    public void RoundTrip_PreservaCentroDeCostosNulo_CuandoLaSedeNoTieneCentroDeCostosAsignado()
    {
        var evento = new SedeDeMarcacionResuelta(
            "EMP-002",
            new DateTime(2026, 3, 15, 8, 9, 0),
            "DEV-002",
            "002",
            "Sede Secundaria",
            null);

        var json = JsonSerializer.Serialize(evento, CrearOpcionesProductor());
        var restaurado = JsonSerializer.Deserialize<SedeDeMarcacionResuelta>(json, CrearOpcionesConsumidor());

        restaurado.Should().Be(evento);
    }
}
