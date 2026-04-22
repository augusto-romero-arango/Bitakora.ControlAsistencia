// Issue #112: Tests de serializacion round-trip para MomentoDelDia.
// Viaja en el evento publico DiaCalculado => contrato critico.
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects.ControlHoras;

/// <summary>
/// MomentoDelDia es un record con primary ctor: STJ lo maneja nativamente.
/// Estos tests verifican el round-trip JSON y preservacion de la igualdad.
/// </summary>
public class MomentoDelDiaSerializacionTests
{
    [Fact]
    public void RoundTrip_PreservaHoraYOffsetCero_CuandoSinOffset()
    {
        var original = new MomentoDelDia(new TimeOnly(8, 30));

        var json = JsonSerializer.Serialize(original);
        var restaurado = JsonSerializer.Deserialize<MomentoDelDia>(json);

        restaurado.Should().Be(original);
        restaurado!.Hora.Should().Be(new TimeOnly(8, 30));
        restaurado.DiaOffset.Should().Be(0);
    }

    [Fact]
    public void RoundTrip_PreservaHoraYOffsetUno_CuandoOffsetEsUno()
    {
        var original = new MomentoDelDia(new TimeOnly(6, 0), 1);

        var json = JsonSerializer.Serialize(original);
        var restaurado = JsonSerializer.Deserialize<MomentoDelDia>(json);

        restaurado.Should().Be(original);
        restaurado!.DiaOffset.Should().Be(1);
        restaurado.MinutosAbsolutos.Should().Be(1800);
    }
}
