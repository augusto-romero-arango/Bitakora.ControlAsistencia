using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.ConsultarAusencias;

public class ConsultarAusenciasSmokeTests(McpFixture mcp)
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ConsultarAusencias_Responde_CuandoSeConsultaUnaVentanaCorta()
    {
        var resultado = await mcp.Cliente.CallToolAsync(
            "consultar_ausencias",
            new Dictionary<string, object?>
            {
                ["desde"] = "2026-10-07",
                ["hasta"] = "2026-10-13",
                ["codigos_colaborador"] = "COL-1"
            },
            cancellationToken: TestContext.Current.CancellationToken);

        resultado.IsError.Should().NotBeTrue();
        var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;

        if (texto.TrimStart().StartsWith('{'))
        {
            using var json = JsonDocument.Parse(texto);
            json.RootElement.GetProperty("desde").GetString().Should().Be("2026-10-07");
            json.RootElement.GetProperty("hasta").GetString().Should().Be("2026-10-13");
        }
        else
        {
            texto.Should().Be("Nadie falta entre 2026-10-07 y 2026-10-13.");
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
