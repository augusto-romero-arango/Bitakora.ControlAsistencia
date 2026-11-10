using AwesomeAssertions;

using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class SubFranjaTests
{
    [Fact]
    public void Crear_RetornaSubFranja_CuandoDatosValidos()
    {
        var franja = SubFranja.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));

        franja.ToString().Should().Be("10:00-10:15");
    }

    [Fact]
    public void Crear_EsSubtipoDeAbstraccionBase_CuandoSeInstancia()
    {
        var franja = SubFranja.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));

        franja.Should().BeAssignableTo<FranjaTemporal>();
    }

    // Inicio == fin no tiene error propio: lo absorbe DuracionNoPositiva.
    [Fact]
    public void Crear_LanzaExcepcion_CuandoInicioYFinSonIguales()
    {
        var act = () => SubFranja.Crear(new TimeOnly(10, 0), new TimeOnly(10, 0));

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.DuracionNoPositiva}*");
    }

    [Fact]
    public void Crear_LanzaExcepcion_CuandoFinEsAnteriorAInicioConOffsetsIguales()
    {
        var act = () => SubFranja.Crear(new TimeOnly(23, 30), new TimeOnly(0, 30));

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.DuracionNoPositiva}*");
    }

    [Fact]
    public void Crear_AceptaFranja_CuandoCruzaMedianocheConOffsetExplicito()
    {
        var franja = SubFranja.Crear(new TimeOnly(23, 30), new TimeOnly(0, 30),
            diaOffsetInicio: 0, diaOffsetFin: 1);

        franja.DuracionEnMinutos().Should().Be(60);
    }

    [Fact]
    public void Crear_RetornaFranjaConOffset_CuandoDiaOffsetInicioYFinSonEspecificados()
    {
        var franja = SubFranja.Crear(new TimeOnly(0, 30), new TimeOnly(0, 45),
            diaOffsetInicio: 1, diaOffsetFin: 1);

        franja.ToString().Should().Be("00:30+1-00:45+1");
    }

    [Fact]
    public void Crear_RetornaFranjaConOffsetSoloEnFin_CuandoCruzaMedianoche()
    {
        var franja = SubFranja.Crear(new TimeOnly(23, 50), new TimeOnly(0, 10),
            diaOffsetInicio: 0, diaOffsetFin: 1);

        franja.ToString().Should().Be("23:50-00:10+1");
    }

    [Fact]
    public void DuracionEnMinutos_Retorna15_CuandoFranjaCorta()
    {
        var franja = SubFranja.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));

        franja.DuracionEnMinutos().Should().Be(15);
    }

    [Fact]
    public void DuracionEnMinutos_Retorna60_CuandoFranjaConOffsetEnAmbosLados()
    {
        var franja = SubFranja.Crear(new TimeOnly(1, 0), new TimeOnly(2, 0),
            diaOffsetInicio: 1, diaOffsetFin: 1);

        franja.DuracionEnMinutos().Should().Be(60);
    }

    [Fact]
    public void DuracionEnMinutos_Retorna20_CuandoFranjaCruzaMedianoche()
    {
        var franja = SubFranja.Crear(new TimeOnly(23, 50), new TimeOnly(0, 10),
            diaOffsetInicio: 0, diaOffsetFin: 1);

        franja.DuracionEnMinutos().Should().Be(20);
    }

    [Fact]
    public void DuracionEnMinutos_Retorna120_CuandoFranjaCruzaMedianocheConRangoMayor()
    {
        var franja = SubFranja.Crear(new TimeOnly(23, 0), new TimeOnly(1, 0),
            diaOffsetInicio: 0, diaOffsetFin: 1);

        franja.DuracionEnMinutos().Should().Be(120);
    }

    [Fact]
    public void DuracionEnHorasDecimales_Retorna0Punto25_CuandoFranja15Minutos()
    {
        var franja = SubFranja.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));

        franja.DuracionEnHorasDecimales().Should().Be(0.25m);
    }

    [Fact]
    public void ToString_MuestraFormatoSinOffset_CuandoAmbosSonDia0()
    {
        var franja = SubFranja.Crear(new TimeOnly(12, 0), new TimeOnly(12, 30));

        franja.ToString().Should().Be("12:00-12:30");
    }

    [Fact]
    public void ToString_MuestraOffsetSoloEnFin_CuandoCruzaMedianoche()
    {
        var franja = SubFranja.Crear(new TimeOnly(23, 45), new TimeOnly(0, 15),
            diaOffsetInicio: 0, diaOffsetFin: 1);

        franja.ToString().Should().Be("23:45-00:15+1");
    }

    [Fact]
    public void ToString_MuestraOffsetEnAmbos_CuandoAmbosOffsetsSonUno()
    {
        var franja = SubFranja.Crear(new TimeOnly(2, 0), new TimeOnly(2, 30),
            diaOffsetInicio: 1, diaOffsetFin: 1);

        franja.ToString().Should().Be("02:00+1-02:30+1");
    }
}
