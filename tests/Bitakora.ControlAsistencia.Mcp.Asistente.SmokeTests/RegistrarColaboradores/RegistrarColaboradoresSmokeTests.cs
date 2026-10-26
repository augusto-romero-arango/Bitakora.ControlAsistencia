using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.RegistrarColaboradores;

public class RegistrarColaboradoresSmokeTests(McpFixture mcp)
{
    private static Dictionary<string, object?> Fila(string numero) => new()
    {
        ["tipo_identificacion"] = "CC",
        ["numero_identificacion"] = numero,
        ["primer_nombre"] = "[TEST]",
        ["primer_apellido"] = "MCP",
        ["codigo_colaborador"] = $"TEST-{Guid.CreateVersion7()}",
        ["fecha_inicio"] = "2026-09-01"
    };

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarColaboradores_RegistraLasFilas_CuandoLosDatosSonValidos()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeros = new[]
        {
            Guid.CreateVersion7().ToString("N").ToUpperInvariant(),
            Guid.CreateVersion7().ToString("N").ToUpperInvariant()
        };
        var lista = JsonSerializer.Serialize(numeros.Select(Fila));

        var resultado = await mcp.Cliente.CallToolAsync(
            "registrar_colaboradores",
            new Dictionary<string, object?> { ["colaboradores"] = lista },
            cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        using var json = Sembrado.LeerJson(resultado);
        json.RootElement.GetProperty("registrados").GetInt32().Should().Be(2);

        var identificacion = $"CC-{numeros[0]}";
        var ficha = await Polling.WaitUntilAsync(async () =>
        {
            var lectura = await mcp.Cliente.CallToolAsync(
                "listar_colaboradores",
                new Dictionary<string, object?> { ["identificacion"] = identificacion },
                cancellationToken: ct);
            var documento = Sembrado.LeerJson(lectura);
            return documento.RootElement.TryGetProperty("identificacion", out _) ? documento : null;
        }, TimeSpan.FromSeconds(60));

        ficha.RootElement.GetProperty("identificacion").GetString().Should().Be(identificacion);
    }
}
