// Issue #112: Tests de serializacion round-trip para IntervaloTemporal.
// Viaja en el evento publico DiaCalculado => contrato critico.
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects.ControlHoras;

/// <summary>
/// IntervaloTemporal es una class con factory static, no record: STJ requiere
/// el atributo [JsonConstructor] en el ctor privado. Estos tests verifican el
/// round-trip JSON (serializar -> deserializar) y preservacion de igualdad.
/// </summary>
public class IntervaloTemporalSerializacionTests
{
    [Fact]
    public void RoundTrip_PreservaValoresYIgualdad_CuandoRangoDiurno()
    {
        var original = IntervaloTemporal.Crear(
            new MomentoDelDia(new TimeOnly(8, 0)),
            new MomentoDelDia(new TimeOnly(17, 0)));

        var json = JsonSerializer.Serialize(original);
        var restaurado = JsonSerializer.Deserialize<IntervaloTemporal>(json);

        restaurado.Should().NotBeNull();
        restaurado.Should().Be(original);
        restaurado!.DuracionEnMinutos.Should().Be(540);
        restaurado.ToString().Should().Be(original.ToString());
    }

    [Fact]
    public void RoundTrip_PreservaOffsetFin_CuandoRangoCruzaMedianoche()
    {
        var original = IntervaloTemporal.Crear(
            new MomentoDelDia(new TimeOnly(22, 0)),
            new MomentoDelDia(new TimeOnly(6, 0), 1));

        var json = JsonSerializer.Serialize(original);
        var restaurado = JsonSerializer.Deserialize<IntervaloTemporal>(json);

        restaurado.Should().NotBeNull();
        restaurado.Should().Be(original);
        restaurado!.Fin.DiaOffset.Should().Be(1);
        restaurado.DuracionEnMinutos.Should().Be(480);
    }
}
