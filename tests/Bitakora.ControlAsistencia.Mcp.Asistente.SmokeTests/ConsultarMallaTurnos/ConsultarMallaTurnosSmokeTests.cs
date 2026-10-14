using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.ConsultarMallaTurnos;

public class ConsultarMallaTurnosSmokeTests(McpFixture mcp)
{
    private static void VerificarSobreOFrase(string texto)
    {
        if (texto.TrimStart().StartsWith('{'))
        {
            using var json = JsonDocument.Parse(texto);
            json.RootElement.GetProperty("desde").GetString().Should().Be("2027-03-01");
            json.RootElement.GetProperty("hasta").GetString().Should().Be("2027-03-07");
            json.RootElement.TryGetProperty("total", out _).Should().BeFalse();
        }
        else
        {
            texto.Should().Contain("2027");
        }
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ConsultarMallaTurnos_Responde_CuandoSeFiltraPorCodigoDeColaborador()
    {
        var resultado = await mcp.Cliente.CallToolAsync(
            "consultar_malla_turnos",
            new Dictionary<string, object?>
            {
                ["fecha"] = "2027-03-03",
                ["codigos_colaborador"] = "COL-1"
            },
            cancellationToken: TestContext.Current.CancellationToken);

        resultado.IsError.Should().NotBeTrue();
        VerificarSobreOFrase(resultado.Content.OfType<TextContentBlock>().Single().Text);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ConsultarMallaTurnos_Responde_CuandoSeFiltraPorUnaSedeExistente()
    {
        var ct = TestContext.Current.CancellationToken;
        var sedes = await mcp.Cliente.CallToolAsync(
            "listar_sedes", new Dictionary<string, object?>(), cancellationToken: ct);
        using var sedesJson = JsonDocument.Parse(sedes.Content.OfType<TextContentBlock>().Single().Text);
        var codigoSede = sedesJson.RootElement.GetProperty("sedes").EnumerateArray()
            .First().GetProperty("codigo").GetString();

        var resultado = await mcp.Cliente.CallToolAsync(
            "consultar_malla_turnos",
            new Dictionary<string, object?>
            {
                ["fecha"] = "2027-03-03",
                ["sede"] = codigoSede
            },
            cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        VerificarSobreOFrase(resultado.Content.OfType<TextContentBlock>().Single().Text);
    }
}
