using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.ConsultarAusencias;

public class ConsultarAusenciasSmokeTests(McpFixture mcp)
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ConsultarAusencias_ConservaLos35Dias_CuandoElPeriodoNoSeRecorta()
    {
        var texto = await Consultar("2026-11-10");

        if (texto.TrimStart().StartsWith('{'))
        {
            using var json = JsonDocument.Parse(texto);
            var raiz = json.RootElement;
            raiz.GetProperty("desde").GetString().Should().Be("2026-10-07");
            raiz.GetProperty("hasta").GetString().Should().Be("2026-11-10");
            raiz.TryGetProperty("nota", out _).Should().BeFalse();
            AfirmarColaboradores(raiz);
        }
        else
        {
            texto.Should().Be("Nadie falta entre 2026-10-07 y 2026-11-10.");
        }
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ConsultarAusencias_InformaElPeriodoAplicado_CuandoSePiden36Dias()
    {
        var texto = await Consultar("2026-11-11");

        if (texto.TrimStart().StartsWith('{'))
        {
            using var json = JsonDocument.Parse(texto);
            var raiz = json.RootElement;
            raiz.GetProperty("desde").GetString().Should().Be("2026-10-07");
            raiz.GetProperty("hasta").GetString().Should().Be("2026-11-10");
            raiz.GetProperty("nota").GetString().Should().Contain(
                "El periodo pedido excedia 35 dias y fue recortado; periodo aplicado: 2026-10-07 a 2026-11-10.");
            AfirmarColaboradores(raiz);
        }
        else
        {
            texto.Should().Be("Nadie falta entre 2026-10-07 y 2026-11-10.");
        }
    }

    private async Task<string> Consultar(string hasta)
    {
        var resultado = await mcp.Cliente.CallToolAsync(
            "consultar_ausencias",
            new Dictionary<string, object?>
            {
                ["desde"] = "2026-10-07",
                ["hasta"] = hasta,
                ["codigos_colaborador"] = "COL-1"
            },
            cancellationToken: TestContext.Current.CancellationToken);

        resultado.IsError.Should().NotBeTrue();
        return resultado.Content.OfType<TextContentBlock>().Single().Text;
    }

    private static void AfirmarColaboradores(JsonElement raiz)
    {
        var mostrando = raiz.GetProperty("mostrando").GetInt32();
        raiz.GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(mostrando);
        raiz.GetProperty("colaboradores").EnumerateArray().ToList().Should().HaveCount(mostrando);
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
