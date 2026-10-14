using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Infraestructura;

public class ValidadorDuracionTests
{
    [Fact]
    public void TryParsear_DevuelveHorasYMinutos_CuandoElFormatoEsHmm()
    {
        ValidadorDuracion.TryParsear("42:00", out var d).Should().BeTrue();
        d.Should().Be(new HorasYMinutosJornada(42, 0));
    }

    [Fact]
    public void TryParsear_AceptaHoraDeUnDigito_CuandoElFormatoEsHmm()
    {
        ValidadorDuracion.TryParsear("8:30", out var d).Should().BeTrue();
        d.Should().Be(new HorasYMinutosJornada(8, 30));
    }

    [Fact]
    public void TryParsear_AceptaElMaximo_CuandoEs999Con59()
    {
        ValidadorDuracion.TryParsear("999:59", out var d).Should().BeTrue();
        d.Should().Be(new HorasYMinutosJornada(999, 59));
    }

    [Fact]
    public void TryParsear_AceptaCero_CuandoEs0Con00()
    {
        ValidadorDuracion.TryParsear("0:00", out var d).Should().BeTrue();
        d.Should().Be(new HorasYMinutosJornada(0, 0));
    }

    [Fact]
    public void TryParsear_Rechaza_CuandoElFormatoEsInvalido()
    {
        foreach (var texto in new string?[] { "8h", "8:75", "8:5", "1000:00", "-1:00", "", "  ", null })
            ValidadorDuracion.TryParsear(texto, out _).Should().BeFalse($"'{texto}' no es H:mm valido");
    }
}
