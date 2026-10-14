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
            texto.Should().StartWith("Nadie falta entre 2026-10-07 y 2026-10-13");
        }
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ConsultarAusencias_Responde_CuandoSeFiltraPorUnaSedeExistente()
    {
        var ct = TestContext.Current.CancellationToken;
        var sedes = await mcp.Cliente.CallToolAsync(
            "listar_sedes", new Dictionary<string, object?>(), cancellationToken: ct);
        using var sedesJson = JsonDocument.Parse(sedes.Content.OfType<TextContentBlock>().Single().Text);
        var codigoSede = sedesJson.RootElement.GetProperty("sedes").EnumerateArray()
            .First().GetProperty("codigo").GetString();

        var resultado = await mcp.Cliente.CallToolAsync(
            "consultar_ausencias",
            new Dictionary<string, object?>
            {
                ["desde"] = "2026-10-07",
                ["hasta"] = "2026-10-13",
                ["sede"] = codigoSede
            },
            cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;

        if (texto.TrimStart().StartsWith('{'))
        {
            using var json = JsonDocument.Parse(texto);
            json.RootElement.GetProperty("desde").GetString().Should().Be("2026-10-07");
            json.RootElement.GetProperty("hasta").GetString().Should().Be("2026-10-13");
            json.RootElement.TryGetProperty("total", out _).Should().BeFalse();
        }
        else
        {
            texto.Should().Contain("2026-10-07").And.Contain("2026-10-13");
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
