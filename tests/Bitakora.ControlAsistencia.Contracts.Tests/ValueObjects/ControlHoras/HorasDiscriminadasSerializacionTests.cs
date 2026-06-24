// Issue #183 CA-1: HorasDiscriminadas existe en Contracts y serializa/deserializa con STJ
// por defecto, SIN resolver custom. Este test vive en Contracts.Tests, que NO puede referenciar
// el dominio ni su ConfiguracionSerializacionControlHoras: probar el roundtrip aqui demuestra
// estructuralmente que el payload no depende de la serializacion interna de ControlHoras.
// Es la barrera de regresion del bug del smoke CA-5 (NullReferenceException por payload lossy).
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects.ControlHoras;

/// <summary>
/// Tests de serializacion de HorasDiscriminadas - payload primitivo del desglose del dia.
/// Interfaz publica: constructor primario (MinutosPorConcepto, Trazabilidad).
/// </summary>
public class HorasDiscriminadasSerializacionTests
{
    // CA-1: roundtrip con el serializador por defecto (sin opciones, PascalCase nativo).
    // Incluye la clave literal "Retardo" para ejercitar el payload completo.
    [Fact]
    public void RoundTrip_PreservaMinutosPorConcepto_ConSerializadorPorDefecto()
    {
        var original = new HorasDiscriminadas(
            new Dictionary<string, int>
            {
                ["OrdinariaDiurna"] = 240,
                ["OrdinariaNocturna"] = 120,
                ["Retardo"] = 30
            },
            []);

        var json = JsonSerializer.Serialize(original);
        var restaurado = JsonSerializer.Deserialize<HorasDiscriminadas>(json);

        restaurado.Should().NotBeNull();
        restaurado!.MinutosPorConcepto.Should().BeEquivalentTo(original.MinutosPorConcepto);
        restaurado.Trazabilidad.Should().BeEmpty();
    }

    // CA-1: roundtrip con el formato del publisher (camelCase, case-insensitive), que mimetiza
    // el canal real de Service Bus (Wolverine serializa camelCase). PropertyNamingPolicy afecta
    // los nombres de propiedad, NO las claves del diccionario: el concepto "DominicalFestivaDiurna"
    // sobrevive intacto, que es justo lo que nomina lee del payload.
    [Fact]
    public void RoundTrip_PreservaClavesDeConcepto_CuandoPublisherUsaCamelCase()
    {
        var publisherOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        var original = new HorasDiscriminadas(
            new Dictionary<string, int> { ["DominicalFestivaDiurna"] = 420 },
            []);

        var json = JsonSerializer.Serialize(original, publisherOptions);
        var restaurado = JsonSerializer.Deserialize<HorasDiscriminadas>(json, publisherOptions);

        restaurado.Should().NotBeNull();
        restaurado!.MinutosPorConcepto.Should().ContainKey("DominicalFestivaDiurna");
        restaurado.MinutosPorConcepto["DominicalFestivaDiurna"].Should().Be(420);
        restaurado.Trazabilidad.Should().BeEmpty();
    }

    // CA-1: roundtrip de un payload vacio (sin conceptos ni trazabilidad) - el caso del dia
    // anomalo o sin turno, donde el desglose discrimina a colecciones vacias.
    [Fact]
    public void RoundTrip_PreservaColeccionesVacias_CuandoNoHayConceptos()
    {
        var original = new HorasDiscriminadas(new Dictionary<string, int>(), []);

        var json = JsonSerializer.Serialize(original);
        var restaurado = JsonSerializer.Deserialize<HorasDiscriminadas>(json);

        restaurado.Should().NotBeNull();
        restaurado!.MinutosPorConcepto.Should().BeEmpty();
        restaurado.Trazabilidad.Should().BeEmpty();
    }
}
