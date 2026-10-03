using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.ConsultarAusencias;

public class ConsultarAusenciasSmokeTests(McpFixture mcp)
{
    // Se afirma forma, no datos puntuales: los datos de dev cambian entre corridas. Sin ausentes la
    // respuesta es el mensaje de "nadie falta" (no JSON), asi que ambas formas son validas.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ConsultarAusencias_DevuelveLasAusenciasDelPeriodo_CuandoDesdeYHastaSonValidos()
    {
        var ct = TestContext.Current.CancellationToken;
        var resultado = await mcp.Cliente.CallToolAsync(
            "consultar_ausencias",
            new Dictionary<string, object?>
            {
                ["desde"] = "2026-09-01",
                ["hasta"] = "2026-09-07",
                ["codigos_colaborador"] = "COL-1"
            },
            cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;

        if (texto.TrimStart().StartsWith('{'))
        {
            using var json = JsonDocument.Parse(texto);
            var raiz = json.RootElement;
            var mostrando = raiz.GetProperty("mostrando").GetInt32();
            raiz.GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(mostrando);
            raiz.GetProperty("colaboradores").EnumerateArray().ToList().Should().HaveCount(mostrando);
        }
        else
        {
            texto.Should().StartWith("Nadie falta entre");
        }
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ConsultarAusencias_RespondeElMensajeDeValidacion_CuandoLaFechaEsInvalida()
    {
        var ct = TestContext.Current.CancellationToken;
        var resultado = await mcp.Cliente.CallToolAsync(
            "consultar_ausencias",
            new Dictionary<string, object?> { ["desde"] = "2026-99-99", ["hasta"] = "2026-01-01" },
            cancellationToken: ct);

        resultado.Content.OfType<TextContentBlock>().Single().Text
            .Should().Be("'desde' debe ser una fecha con formato yyyy-MM-dd; llego '2026-99-99'.");
    }
}
