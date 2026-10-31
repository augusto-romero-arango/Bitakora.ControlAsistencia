using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Infraestructura;

public class NotacionDuracionTests
{
    [Fact]
    public void Texto_MuestraSoloHoras_CuandoNoHayMinutosSueltos()
    {
        NotacionDuracion.Texto(120).Should().Be("2 h");
    }

    [Fact]
    public void Texto_MuestraHorasYMinutos_CuandoHayAmbos()
    {
        NotacionDuracion.Texto(390).Should().Be("6 h 30 min");
    }

    [Fact]
    public void Texto_MuestraSoloMinutos_CuandoDuraMenosDeUnaHora()
    {
        NotacionDuracion.Texto(30).Should().Be("30 min");
    }
}
