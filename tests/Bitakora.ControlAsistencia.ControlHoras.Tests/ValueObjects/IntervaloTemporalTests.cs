using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ValueObjects;

public class IntervaloTemporalTests
{
    private static readonly MomentoDelDia Las8 = new(new TimeOnly(8, 0));
    private static readonly MomentoDelDia Las17 = new(new TimeOnly(17, 0));
    private static readonly MomentoDelDia Las22 = new(new TimeOnly(22, 0));
    private static readonly MomentoDelDia Las6SiguienteDia = new(new TimeOnly(6, 0), 1);
    private static readonly DateOnly FechaBase = new(2026, 3, 15);

    [Fact]
    public void DuracionEnMinutos_Retorna540_CuandoRango8A17()
    {
        var intervalo = IntervaloTemporal.Crear(Las8, Las17);

        intervalo.DuracionEnMinutos.Should().Be(540);
    }

    [Fact]
    public void DuracionEnMinutos_Retorna480_CuandoRangoNocturno22A6SiguienteDia()
    {
        // 22:00+0 = 1320 min, 06:00+1 = 1800 min => 1800 - 1320 = 480
        var intervalo = IntervaloTemporal.Crear(Las22, Las6SiguienteDia);

        intervalo.DuracionEnMinutos.Should().Be(480);
    }

    [Fact]
    public void DuracionEnHorasDecimales_Retorna0Punto25Decimal_CuandoFranjaDe15Minutos()
    {
        // 15 minutos / 60 = 0.25m exacto - verificar que es decimal y no double
        var inicio = new MomentoDelDia(new TimeOnly(8, 0));
        var fin = new MomentoDelDia(new TimeOnly(8, 15));
        var intervalo = IntervaloTemporal.Crear(inicio, fin);

        intervalo.DuracionEnHorasDecimales.Should().Be(0.25m);
    }

    [Fact]
    public void DuracionEnHorasDecimales_Retorna9Decimal_CuandoRango8A17()
    {
        var intervalo = IntervaloTemporal.Crear(Las8, Las17);

        intervalo.DuracionEnHorasDecimales.Should().Be(9m);
    }

    [Fact]
    public void Crear_LanzaExcepcion_CuandoInicioEsIgualAFin()
    {
        var momento = new MomentoDelDia(new TimeOnly(10, 0));

        var act = () => IntervaloTemporal.Crear(momento, momento);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{IntervaloTemporal.Mensajes.InicioDebeSerMenorQueFin}*");
    }

    [Fact]
    public void Crear_LanzaExcepcion_CuandoInicioEsMayorQueFin()
    {
        // 17:00+0 = 1020 min > 08:00+0 = 480 min
        var act = () => IntervaloTemporal.Crear(Las17, Las8);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{IntervaloTemporal.Mensajes.InicioDebeSerMenorQueFin}*");
    }

    [Fact]
    public void ToString_MuestraFormatoConDuracion_CuandoRangoDiurnoSinOffset()
    {
        var intervalo = IntervaloTemporal.Crear(Las8, Las17);

        intervalo.ToString().Should().Be("08:00-17:00 (540min)");
    }

    [Fact]
    public void ToString_MuestraOffsetEnFinYDuracion_CuandoRangoNocturno()
    {
        var intervalo = IntervaloTemporal.Crear(Las22, Las6SiguienteDia);

        intervalo.ToString().Should().Be("22:00-06:00+1 (480min)");
    }

    [Fact]
    public void ResolverA_RetornaTuplaDeDateTimesCorrectaParaRangoDiurno()
    {
        var intervalo = IntervaloTemporal.Crear(Las8, Las17);

        var (inicio, fin) = intervalo.ResolverA(FechaBase);

        inicio.Should().Be(new DateTime(2026, 3, 15, 8, 0, 0));
        fin.Should().Be(new DateTime(2026, 3, 15, 17, 0, 0));
    }

    [Fact]
    public void ResolverA_RetornaTuplaConDiaSiguienteEnFin_CuandoRangoNocturno()
    {
        var intervalo = IntervaloTemporal.Crear(Las22, Las6SiguienteDia);

        var (inicio, fin) = intervalo.ResolverA(FechaBase);

        inicio.Should().Be(new DateTime(2026, 3, 15, 22, 0, 0));
        fin.Should().Be(new DateTime(2026, 3, 16, 6, 0, 0));
    }
}
