using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Infraestructura;

public class CoincidenciaDeTextoTests
{
    [Fact]
    public void Evaluar_CoincideEnDosPalabras_CuandoSePideNombreIncompletoEnOtroOrden()
    {
        var resultado = CoincidenciaDeTexto.Evaluar("vigilancia noct", "Turno Nocturno Vigilancia");

        resultado.Should().HaveCount(2);
        resultado.Select(p => p.Tipo).Should().Equal(
            TipoCoincidenciaDePalabra.Exacta, TipoCoincidenciaDePalabra.Prefijo);
        resultado[1].Encontrada.Should().Be("nocturno");
    }

    [Fact]
    public void Evaluar_CoincideAUnaLetra_CuandoLaPalabraTieneUnErrorDeDigitacion()
    {
        var resultado = CoincidenciaDeTexto.Evaluar("vijilancia", "Turno Vigilancia");

        resultado.Should().ContainSingle().Which.Tipo.Should().Be(TipoCoincidenciaDePalabra.UnaLetra);
    }

    [Fact]
    public void Evaluar_CoincideExacta_CuandoSoloDifierenAcentosYMayusculas()
    {
        var resultado = CoincidenciaDeTexto.Evaluar("Cocína", "cocina");

        resultado.Should().ContainSingle().Which.Tipo.Should().Be(TipoCoincidenciaDePalabra.Exacta);
    }

    [Fact]
    public void Evaluar_CoincideComoPrefijo_CuandoLaPalabraCortaEmpiezaElTexto()
    {
        var resultado = CoincidenciaDeTexto.Evaluar("de", "Turno del Norte");

        resultado.Should().ContainSingle().Which.Tipo.Should().Be(TipoCoincidenciaDePalabra.Prefijo);
    }

    [Fact]
    public void Evaluar_NoCoincide_CuandoPalabraDeMenosDeCuatroLetrasDifiereEnUna()
    {
        var resultado = CoincidenciaDeTexto.Evaluar("de", "Turno da Norte");

        resultado.Should().BeEmpty();
    }

    [Fact]
    public void Evaluar_NoCoincide_CuandoNingunaPalabraSeParece()
    {
        var resultado = CoincidenciaDeTexto.Evaluar("panaderia", "Turno Nocturno Vigilancia");

        resultado.Should().BeEmpty();
    }
}
