// Issue #2: Tests de round-trip JSON para FranjaOrdinaria
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects;

public class FranjaOrdinariaSerializacionTests
{
    private static JsonSerializerOptions CrearOpciones()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        SubFranja.ConfigurarSerializacion(resolver);
        FranjaOrdinaria.ConfigurarSerializacion(resolver);
        return new JsonSerializerOptions { TypeInfoResolver = resolver };
    }

    [Fact]
    public void RoundTrip_PreservaValores_CuandoOrdinariaSinHijos()
    {
        var original = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0));
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<FranjaOrdinaria>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.DuracionEnMinutos().Should().Be(360);
        restaurado.ToString().Should().Be("(06:00-12:00)");
    }

    [Fact]
    public void RoundTrip_PreservaValores_CuandoOrdinariaNocturnaConOffset()
    {
        var original = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0), diaOffsetFin: 1);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<FranjaOrdinaria>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.DuracionEnMinutos().Should().Be(480);
        restaurado.ToString().Should().Be("(22:00-06:00+1)");
    }

    [Fact]
    public void RoundTrip_PreservaValores_CuandoOrdinariaConDescansoYExtra()
    {
        var descanso = SubFranja.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));
        var extra = SubFranja.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));
        var original = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0),
            descansos: [descanso], extras: [extra]);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<FranjaOrdinaria>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.ToString().Should().Be("(06:00-12:00)[Descansos:(10:00-10:15)][Extras:(06:00-08:00)]");
    }

    [Fact]
    public void RoundTrip_PreservaValores_CuandoOrdinariaNocturnaConHijasConOffsets()
    {
        var descanso = SubFranja.Crear(new TimeOnly(23, 0), new TimeOnly(23, 15),
            diaOffsetInicio: 0, diaOffsetFin: 0);
        var extra = SubFranja.Crear(new TimeOnly(4, 0), new TimeOnly(5, 0),
            diaOffsetInicio: 1, diaOffsetFin: 1);
        var original = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0), diaOffsetFin: 1,
            descansos: [descanso], extras: [extra]);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<FranjaOrdinaria>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.ToString().Should().Be(original.ToString());
        restaurado.DuracionEnMinutos().Should().Be(original.DuracionEnMinutos());
    }

    [Fact]
    public void RoundTrip_PreservaIgualdad_CuandoOrdinariaSinHijosRestaurada()
    {
        var original = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0));
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<FranjaOrdinaria>(json, opciones);

        restaurado.Should().Be(original);
    }
}
