using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Infraestructura;

public class RespuestaJsonTests
{
    [Fact]
    public void Serializar_ConservaElSignoMasLiteral_CuandoElHorarioTieneOffsetDeDia()
    {
        var texto = RespuestaJson.Serializar(new { horario = "22:00-06:00+1" });

        texto.Should().Contain("\"22:00-06:00+1\"");
        texto.Should().NotContain("\\u002B");
    }

    [Fact]
    public void Serializar_ConservaTildesYEnieLiterales_CuandoElTextoLosTiene()
    {
        var texto = RespuestaJson.Serializar(new { nota = "Ñoño programación" });

        texto.Should().Contain("Ñoño programación");
        texto.Should().NotContain("\\u");
    }

    [Fact]
    public void Serializar_ConservaCaracteresSensiblesParaHtmlLiterales_CuandoElTextoLosTiene()
    {
        var texto = RespuestaJson.Serializar(new { nota = "<a> & 'x'" });

        texto.Should().Contain("<a> & 'x'");
        texto.Should().NotContain("\\u003C");
        texto.Should().NotContain("\\u0026");
        texto.Should().NotContain("\\u0027");
    }

    [Fact]
    public void Serializar_EscapaComillasDoblesYVuelveAParsear_CuandoElStringLasTiene()
    {
        const string original = "dijo \"hola\" + <b> & 'x'";

        var texto = RespuestaJson.Serializar(new { nota = original });

        texto.Should().Contain("\\\"hola\\\"");
        JsonNode.Parse(texto)!["nota"]!.GetValue<string>().Should().Be(original);
    }
}
