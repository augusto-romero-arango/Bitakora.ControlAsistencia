using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.AsignarEtiqueta;

public class AsignarEtiquetaSmokeTests(McpFixture mcp)
{
    private static readonly TimeSpan TimeoutPolling = TimeSpan.FromSeconds(60);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_LaMuestraEnListarColaboradoresYLuegoLaRetira_CuandoSeUsaUnColaboradorRegistrado()
    {
        var ct = TestContext.Current.CancellationToken;
        var (identificacion, _) = await Sembrado.RegistrarColaboradorAsync(mcp.Cliente, null, ct);
        var valor = $"[TEST] {Guid.CreateVersion7()}";

        using var asignada = await Polling.WaitUntilAsync(
            async () =>
            {
                var respuesta = await mcp.Cliente.CallToolAsync(
                    "asignar_etiqueta",
                    new Dictionary<string, object?>
                    {
                        ["categoria"] = "smoke",
                        ["valor"] = valor,
                        ["identificaciones"] = identificacion
                    },
                    cancellationToken: ct);
                var documento = Sembrado.LeerJson(respuesta);
                return documento.RootElement.TryGetProperty("asignados", out _)
                    && !documento.RootElement.TryGetProperty("no_encontrados", out _)
                    ? documento
                    : null;
            },
            TimeoutPolling);

        asignada.RootElement.GetProperty("asignados").GetInt32().Should().Be(1);

        using var listada = await Polling.WaitUntilAsync(
            async () =>
            {
                var respuesta = await mcp.Cliente.CallToolAsync(
                    "listar_colaboradores",
                    new Dictionary<string, object?> { ["identificacion"] = identificacion },
                    cancellationToken: ct);
                var documento = Sembrado.LeerJson(respuesta);
                return documento.RootElement.ValueKind == JsonValueKind.Object
                    && documento.RootElement.GetRawText().Contains(valor, StringComparison.OrdinalIgnoreCase)
                    ? documento
                    : null;
            },
            TimeoutPolling);

        listada.RootElement.GetRawText().Should().Contain("smoke");

        var retirada = await mcp.Cliente.CallToolAsync(
            "retirar_etiqueta",
            new Dictionary<string, object?> { ["categoria"] = "smoke", ["identificaciones"] = identificacion },
            cancellationToken: ct);

        using var json = Sembrado.LeerJson(retirada);
        json.RootElement.GetProperty("retirados").GetInt32().Should().Be(1);
    }
}
