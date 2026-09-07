using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Infraestructura;

public class DiaSemanaMcpTests
{
    [Fact]
    public void TryParsear_DevuelveUno_CuandoElValorEsLunesEnMinusculas()
    {
        var resultado = DiaSemanaMcp.TryParsear("lunes", out var numeroIso);

        resultado.Should().BeTrue();
        numeroIso.Should().Be(1);
    }

    [Fact]
    public void TryParsear_DevuelveSiete_CuandoElValorEsDomingoEnMayusculas()
    {
        var resultado = DiaSemanaMcp.TryParsear("DOMINGO", out var numeroIso);

        resultado.Should().BeTrue();
        numeroIso.Should().Be(7);
    }

    // CA-3: "Miércoles", "miercoles" y "3" son el mismo dia -- acentos no significativos aqui, a
    // diferencia del nombre de turno/plantilla donde si lo son.
    [Fact]
    public void TryParsear_DevuelveTres_CuandoElValorTieneTildeYMayusculaInicial()
    {
        var resultado = DiaSemanaMcp.TryParsear("Miércoles", out var numeroIso);

        resultado.Should().BeTrue();
        numeroIso.Should().Be(3);
    }

    [Fact]
    public void TryParsear_DevuelveTres_CuandoElValorNoTieneTilde()
    {
        var resultado = DiaSemanaMcp.TryParsear("miercoles", out var numeroIso);

        resultado.Should().BeTrue();
        numeroIso.Should().Be(3);
    }

    [Fact]
    public void TryParsear_DevuelveTres_CuandoElValorEsNumerico()
    {
        var resultado = DiaSemanaMcp.TryParsear("3", out var numeroIso);

        resultado.Should().BeTrue();
        numeroIso.Should().Be(3);
    }

    [Fact]
    public void TryParsear_DevuelveUno_CuandoElValorNumericoTieneEspaciosAlrededor()
    {
        var resultado = DiaSemanaMcp.TryParsear("  1 ", out var numeroIso);

        resultado.Should().BeTrue();
        numeroIso.Should().Be(1);
    }

    [Fact]
    public void TryParsear_DevuelveFalse_CuandoElValorEsUnDiaInexistente()
    {
        var resultado = DiaSemanaMcp.TryParsear("funes", out _);

        resultado.Should().BeFalse();
    }

    [Fact]
    public void TryParsear_DevuelveFalse_CuandoElNumeroEsOcho()
    {
        var resultado = DiaSemanaMcp.TryParsear("8", out _);

        resultado.Should().BeFalse();
    }

    [Fact]
    public void TryParsear_DevuelveFalse_CuandoElNumeroEsCero()
    {
        var resultado = DiaSemanaMcp.TryParsear("0", out _);

        resultado.Should().BeFalse();
    }

    [Fact]
    public void TryParsear_DevuelveFalse_CuandoElValorEstaEnBlanco()
    {
        var resultado = DiaSemanaMcp.TryParsear("   ", out _);

        resultado.Should().BeFalse();
    }
}
