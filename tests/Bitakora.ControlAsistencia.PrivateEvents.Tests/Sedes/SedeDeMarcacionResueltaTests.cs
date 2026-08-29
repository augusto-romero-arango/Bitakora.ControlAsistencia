// Issue #467 CA-6: guardrail de portabilidad por el bus para SedeDeMarcacionResuelta.

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Sedes;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.Sedes;

/// <summary>
/// Verifica que SedeDeMarcacionResuelta (IPrivateEvent) sobrevive el cruce fisico del ASB interno
/// del BC (MEF-ADR-0024 decision #3): el productor (la reaccion de Sedes, via Wolverine) serializa
/// en camelCase; el consumidor (ControlHoras en #463) deserializa con PropertyNameCaseInsensitive,
/// sin resolver custom. Distinto de un round-trip de Marten (seccion 6d del test-writer): aqui la
/// expectativa es que el payload SOBREVIVA sin resolver -- si no sobreviviera, el tipo no seria
/// portable (MEF-ADR-0023). Mismo patron que RegistroDeMarcacionCreadoTests (ControlHoras).
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
        // VO anidado (MEF-ADR-0012).
        restaurado.Should().Be(evento);
    }

    // CA-3 del issue: la sede resuelta puede no tener centro de costos asignado.
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
