using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.ProgramarAusencia;

public class AusenciasSmokeTests(McpFixture mcp)
{
    private static readonly TimeSpan TimeoutPolling = TimeSpan.FromSeconds(30);

    // CA-7: tool call real de programar_ausencia y cancelar_ausencia contra dev. El colaborador se
    // siembra con vinculacion desde 2026-09-01; el assert vive dentro del polling por el lifecycle
    // Async del directorio (ver Fixtures/Polling.cs).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task Ausencia_SeProgramaYSeCancela_CuandoLosDiasEstanEnLaVinculacion()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7();
        var numeroIdentificacion = sufijo.ToString("N").ToUpperInvariant();
        var identificacion = $"CC-{numeroIdentificacion}";

        await mcp.Cliente.CallToolAsync(
            "registrar_colaborador",
            new Dictionary<string, object?>
            {
                ["tipo_identificacion"] = "CC",
                ["numero_identificacion"] = numeroIdentificacion,
                ["primer_nombre"] = "[TEST]",
                ["primer_apellido"] = "MCP",
                ["codigo_colaborador"] = $"TEST-{sufijo}",
                ["fecha_inicio"] = "2026-09-01"
            },
            cancellationToken: ct);

        var programada = await Polling.WaitUntilAsync(
            async () =>
            {
                var respuesta = await mcp.Cliente.CallToolAsync(
                    "programar_ausencia",
                    new Dictionary<string, object?>
                    {
                        ["identificacion"] = identificacion,
                        ["desde"] = "2026-09-10",
                        ["hasta"] = "2026-09-14",
                        ["motivo"] = "Vacaciones"
                    },
                    cancellationToken: ct);
                var candidato = JsonDocument.Parse(respuesta.Content.OfType<TextContentBlock>().Single().Text);
                if (candidato.RootElement.TryGetProperty("motivo", out _))
                    return candidato;
                candidato.Dispose();
                return null;
            },
            TimeoutPolling);
        using var programadaDisponible = programada;
        programada.RootElement.GetProperty("motivo").GetString().Should().Be("Vacaciones");

        var cancelada = await mcp.Cliente.CallToolAsync(
            "cancelar_ausencia",
            new Dictionary<string, object?>
            {
                ["identificacion"] = identificacion,
                ["desde"] = "2026-09-10",
                ["hasta"] = "2026-09-14"
            },
            cancellationToken: ct);
        using var documento = JsonDocument.Parse(cancelada.Content.OfType<TextContentBlock>().Single().Text);
        documento.RootElement.GetProperty("canceladas").EnumerateArray().Should().ContainSingle();
    }
}
