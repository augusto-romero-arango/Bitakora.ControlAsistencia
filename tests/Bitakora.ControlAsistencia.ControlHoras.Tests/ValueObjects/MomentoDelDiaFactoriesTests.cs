using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ValueObjects;

public class MomentoDelDiaFactoriesTests
{
    [Fact]
    public void Desde_RetornaMomentoConOffsetCero_CuandoFechaYAnclaCoinciden()
    {
        var momento = MomentoDelDia.Desde(
            new DateTime(2026, 3, 15, 8, 0, 0),
            new DateOnly(2026, 3, 15));

        momento.Should().Be(new MomentoDelDia(new TimeOnly(8, 0), 0));
    }

    [Fact]
    public void Desde_RetornaMomentoConOffsetUno_CuandoFechaEsUnDiaDespuesDeAncla()
    {
        var momento = MomentoDelDia.Desde(
            new DateTime(2026, 3, 16, 6, 0, 0),
            new DateOnly(2026, 3, 15));

        momento.Should().Be(new MomentoDelDia(new TimeOnly(6, 0), 1));
    }

    [Fact]
    public void DesdeMinutosAbsolutos_Retorna8En0_CuandoSonLas480()
    {
        // 480 / 1440 = 0 dias, 480 % 1440 = 480 min = 8 horas
        var momento = MomentoDelDia.DesdeMinutosAbsolutos(480);

        momento.Should().Be(new MomentoDelDia(new TimeOnly(8, 0), 0));
    }

    [Fact]
    public void DesdeMinutosAbsolutos_Retorna6En1_CuandoSonLas1800()
    {
        // 1800 / 1440 = 1 dia, 1800 % 1440 = 360 min = 6 horas
        var momento = MomentoDelDia.DesdeMinutosAbsolutos(1800);

        momento.Should().Be(new MomentoDelDia(new TimeOnly(6, 0), 1));
    }

    [Fact]
    public void DesdeMinutosAbsolutos_LanzaArgumentException_CuandoMinutosSonNegativos()
    {
        var act = () => MomentoDelDia.DesdeMinutosAbsolutos(-1);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{MomentoDelDia.Mensajes.MinutosAbsolutosDebeSerPositivoOCero}*");
    }

    [Fact]
    public void DesdeMinutosAbsolutos_EsInversoDeMinutosAbsolutos_ParaVariosMomentos()
    {
        var casos = new[]
        {
            new MomentoDelDia(new TimeOnly(8, 0)),          // 480
            new MomentoDelDia(new TimeOnly(6, 0), 1),       // 1800
            new MomentoDelDia(new TimeOnly(22, 0)),          // 1320
            new MomentoDelDia(new TimeOnly(0, 0), 0),       // 0
            new MomentoDelDia(new TimeOnly(23, 59)),         // 1439
        };

        foreach (var esperado in casos)
            MomentoDelDia.DesdeMinutosAbsolutos(esperado.MinutosAbsolutos).Should().Be(esperado);
    }
}
