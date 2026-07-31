// Issue #270 CA-2: guardrail de portabilidad por el bus para RegistroDeMarcacionCreado.

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.ControlHoras;

/// <summary>
/// Verifica que RegistroDeMarcacionCreado (IPrivateEvent) sobrevive el cruce fisico del ASB interno
/// del BC (MEF-ADR-0024 decision #3): el productor (RegistrarMarcacionCommandHandler, via Wolverine)
/// serializa en camelCase; el consumidor (ServiceBusDeserializador, en el Function App de
/// ControlHoras) deserializa con PropertyNameCaseInsensitive=true y SIN resolver custom. Distinto de
/// un round-trip de Marten (seccion 6d del test-writer): aqui la expectativa es que el payload
/// SOBREVIVA sin resolver -- si no sobreviviera, el tipo no seria portable (MEF-ADR-0023).
///
/// Este test compila referenciando UNICAMENTE PrivateEvents (CA-ADR-0029 seccion 3: si necesitara
/// mas, el tipo no seria portable). Replica las opciones manualmente (camelCase al serializar,
/// case-insensitive al leer) porque ServiceBusDeserializador vive en el Function App de ControlHoras,
/// fuera del alcance de este proyecto de test.
/// </summary>
public class RegistroDeMarcacionCreadoTests
{
    // Simula al productor: Wolverine serializa en camelCase por defecto al publicar al Service Bus.
    private static JsonSerializerOptions CrearOpcionesProductor() =>
        new(JsonSerializerDefaults.Web);

    // Simula al consumidor: ServiceBusDeserializador usa PropertyNameCaseInsensitive, sin resolver custom.
    private static JsonSerializerOptions CrearOpcionesConsumidor() =>
        new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConDatosCompletos()
    {
        var evento = new RegistroDeMarcacionCreado(
            "EMP-001", new DateTime(2026, 3, 15, 8, 9, 0), "ENTRADA", "DEV-001");

        var json = JsonSerializer.Serialize(evento, CrearOpcionesProductor());
        var restaurado = JsonSerializer.Deserialize<RegistroDeMarcacionCreado>(json, CrearOpcionesConsumidor());

        restaurado.Should().NotBeNull();
        restaurado.Should().Be(evento);
    }

    [Fact]
    public void RoundTrip_PreservaCamposOpcionalesNulos_CuandoEstanAusentesDelJson()
    {
        var evento = new RegistroDeMarcacionCreado(
            "EMP-002", new DateTime(2026, 3, 15, 8, 9, 0), null, null);

        // Con JsonSerializerDefaults.Web (JsonIgnoreCondition.WhenWritingNull por defecto NO aplica),
        // simulamos el JSON minimo que Wolverine podria producir si los opcionales estuvieran ausentes.
        var json = """
            {
              "empleadoId": "EMP-002",
              "timestampNormalizado": "2026-03-15T08:09:00"
            }
            """;

        var restaurado = JsonSerializer.Deserialize<RegistroDeMarcacionCreado>(json, CrearOpcionesConsumidor());

        restaurado.Should().NotBeNull();
        restaurado.Should().Be(evento);
        restaurado!.TipoMarcacion.Should().BeNull();
        restaurado.DispositivoId.Should().BeNull();
    }
}
