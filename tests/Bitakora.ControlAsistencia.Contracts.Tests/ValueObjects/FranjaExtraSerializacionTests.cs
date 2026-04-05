// Issue #2: Tests de round-trip JSON para FranjaExtra
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects;

public class FranjaExtraSerializacionTests
{
    private static JsonSerializerOptions CrearOpciones()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        FranjaExtra.ConfigurarSerializacion(resolver);
        return new JsonSerializerOptions { TypeInfoResolver = resolver };
    }

    [Fact]
    public void RoundTrip_PreservaValores_CuandoExtraSinOffset()
    {
        var original = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<FranjaExtra>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.DuracionEnMinutos().Should().Be(original.DuracionEnMinutos());
        restaurado.ToString().Should().Be(original.ToString());
    }

    [Fact]
    public void RoundTrip_PreservaValores_CuandoExtraConOffsets()
    {
        var original = FranjaExtra.Crear(new TimeOnly(4, 0), new TimeOnly(6, 0),
            diaOffsetInicio: 1, diaOffsetFin: 1);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<FranjaExtra>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.DuracionEnMinutos().Should().Be(120);
        restaurado.ToString().Should().Be("(04:00+1-06:00+1)");
    }

    [Fact]
    public void RoundTrip_PreservaValores_CuandoExtraCruzaMedianoche()
    {
        var original = FranjaExtra.Crear(new TimeOnly(23, 0), new TimeOnly(1, 0),
            diaOffsetInicio: 0, diaOffsetFin: 1);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<FranjaExtra>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.DuracionEnMinutos().Should().Be(120);
        restaurado.ToString().Should().Be("(23:00-01:00+1)");
    }

    [Fact]
    public void RoundTrip_PreservaIgualdad_CuandoExtraRestaurada()
    {
        var original = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<FranjaExtra>(json, opciones);

        restaurado.Should().Be(original);
    }
}
