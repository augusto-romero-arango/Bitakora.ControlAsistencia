// Issue #112: Crear value objects MomentoDelDia e IntervaloTemporal
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ValueObjects;

/// <summary>
/// Tests de MomentoDelDia - punto en el tiempo con offset de dia opcional.
/// Interfaz publica: constructor, Hora, DiaOffset, MinutosAbsolutos, ResolverA(), ToString(), IComparable.
/// </summary>
public class MomentoDelDiaTests
{
    // ---------- CA-1: MinutosAbsolutos sin offset ----------

    [Fact]
    public void MinutosAbsolutos_Retorna480_CuandoHoraEsLas8SinOffset()
    {
        var momento = new MomentoDelDia(new TimeOnly(8, 0));

        momento.MinutosAbsolutos.Should().Be(480);
    }

    // ---------- CA-2: MinutosAbsolutos con offset 1 ----------

    [Fact]
    public void MinutosAbsolutos_Retorna1800_CuandoHoraEsLas6ConOffsetUno()
    {
        // 6 * 60 + 1 * 1440 = 360 + 1440 = 1800
        var momento = new MomentoDelDia(new TimeOnly(6, 0), 1);

        momento.MinutosAbsolutos.Should().Be(1800);
    }

    // ---------- CA-3: ResolverA sin offset ----------

    [Fact]
    public void ResolverA_Retorna20260315Las22_CuandoHoraEsLas22SinOffset()
    {
        var momento = new MomentoDelDia(new TimeOnly(22, 0));
        var fecha = new DateOnly(2026, 3, 15);

        var resultado = momento.ResolverA(fecha);

        resultado.Should().Be(new DateTime(2026, 3, 15, 22, 0, 0));
    }

    // ---------- CA-4: ResolverA con offset 1 ----------

    [Fact]
    public void ResolverA_Retorna20260316Las6_CuandoHoraEsLas6ConOffsetUno()
    {
        var momento = new MomentoDelDia(new TimeOnly(6, 0), 1);
        var fecha = new DateOnly(2026, 3, 15);

        var resultado = momento.ResolverA(fecha);

        resultado.Should().Be(new DateTime(2026, 3, 16, 6, 0, 0));
    }

    // ---------- CA-5: ToString sin offset ----------

    [Fact]
    public void ToString_Retorna08Colon00_CuandoSinOffset()
    {
        var momento = new MomentoDelDia(new TimeOnly(8, 0));

        momento.ToString().Should().Be("08:00");
    }

    // ---------- CA-6: ToString con offset 1 ----------

    [Fact]
    public void ToString_Retorna06Colon00MasUno_CuandoOffsetEsUno()
    {
        var momento = new MomentoDelDia(new TimeOnly(6, 0), 1);

        momento.ToString().Should().Be("06:00+1");
    }

    [Fact]
    public void ToString_MuestraFormatoConCerosALaIzquierda_CuandoHoraEsMenorDe10()
    {
        var momento = new MomentoDelDia(new TimeOnly(6, 0));

        momento.ToString().Should().Be("06:00");
    }

    // ---------- CA-8: IComparable por MinutosAbsolutos ----------

    [Fact]
    public void CompareTo_RetornaPositivo_CuandoMomentoConOffsetEsMayorQueMomentoNocturnoSinOffset()
    {
        // 06:00+1 = 1800 minutos > 22:00+0 = 1320 minutos
        var momentoConOffset = new MomentoDelDia(new TimeOnly(6, 0), 1);
        var momentoSinOffset = new MomentoDelDia(new TimeOnly(22, 0));

        momentoConOffset.CompareTo(momentoSinOffset).Should().BePositive();
    }

    [Fact]
    public void CompareTo_RetornaNegativo_CuandoMomentoEsMenorQueOtro()
    {
        var menor = new MomentoDelDia(new TimeOnly(8, 0));  // 480 minutos
        var mayor = new MomentoDelDia(new TimeOnly(12, 0)); // 720 minutos

        menor.CompareTo(mayor).Should().BeNegative();
    }

    [Fact]
    public void CompareTo_RetornaCero_CuandoLosMomentosIguales()
    {
        var a = new MomentoDelDia(new TimeOnly(8, 0));
        var b = new MomentoDelDia(new TimeOnly(8, 0));

        a.CompareTo(b).Should().Be(0);
    }

    [Fact]
    public void CompareTo_EsConsistenteConMinutosAbsolutos_CuandoSeMixeanOffsets()
    {
        // 23:00+0 = 1380 minutos
        // 00:30+1 = 1440 + 30 = 1470 minutos => 00:30+1 > 23:00+0
        var nocturno = new MomentoDelDia(new TimeOnly(23, 0));
        var madrugadaSiguiente = new MomentoDelDia(new TimeOnly(0, 30), 1);

        madrugadaSiguiente.CompareTo(nocturno).Should().BePositive();
    }
}
