// Issue #2: Tests de round-trip JSON para FranjaDescanso
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects;

public class FranjaDescansoSerializacionTests
{
    private static JsonSerializerOptions CrearOpciones()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        FranjaDescanso.ConfigurarSerializacion(resolver);
        return new JsonSerializerOptions { TypeInfoResolver = resolver };
    }

    [Fact]
    public void RoundTrip_PreservaValores_CuandoDescansoSinOffset()
    {
        var original = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<FranjaDescanso>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.DuracionEnMinutos().Should().Be(original.DuracionEnMinutos());
        restaurado.ToString().Should().Be(original.ToString());
    }

    [Fact]
    public void RoundTrip_PreservaValores_CuandoDescansoCruzaMedianoche()
    {
        var original = FranjaDescanso.Crear(new TimeOnly(23, 50), new TimeOnly(0, 10),
            diaOffsetInicio: 0, diaOffsetFin: 1);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<FranjaDescanso>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.DuracionEnMinutos().Should().Be(20);
        restaurado.ToString().Should().Be("(23:50-00:10+1)");
    }

    [Fact]
    public void RoundTrip_PreservaValores_CuandoDescansoConAmbosOffsets()
    {
        var original = FranjaDescanso.Crear(new TimeOnly(1, 0), new TimeOnly(1, 30),
            diaOffsetInicio: 1, diaOffsetFin: 1);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<FranjaDescanso>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.DuracionEnMinutos().Should().Be(30);
        restaurado.ToString().Should().Be("(01:00+1-01:30+1)");
    }

    [Fact]
    public void RoundTrip_PreservaIgualdad_CuandoDescansoRestaurado()
    {
        var original = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<FranjaDescanso>(json, opciones);

        restaurado.Should().Be(original);
    }
}
