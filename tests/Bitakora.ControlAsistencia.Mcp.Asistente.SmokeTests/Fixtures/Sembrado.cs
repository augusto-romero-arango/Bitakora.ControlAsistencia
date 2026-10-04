using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;

public static class Sembrado
{
    public const string FechaInicio = "2026-09-01";

    public static async Task<(string Identificacion, string Codigo)> RegistrarColaboradorAsync(
        McpClient cliente, string? codigoSede, CancellationToken ct)
    {
        var numero = Guid.CreateVersion7().ToString("N").ToUpperInvariant();
        var codigo = $"TEST-{Guid.CreateVersion7()}";
        var argumentos = new Dictionary<string, object?>
        {
            ["tipo_identificacion"] = "CC",
            ["numero_identificacion"] = numero,
            ["primer_nombre"] = "[TEST]",
            ["primer_apellido"] = "MCP",
            ["codigo_colaborador"] = codigo,
            ["fecha_inicio"] = FechaInicio,
            ["codigo_sede"] = codigoSede
        };

        var resultado = await cliente.CallToolAsync("registrar_colaborador", argumentos, cancellationToken: ct);
        if (resultado.IsError == true)
            throw new InvalidOperationException("No se pudo sembrar el colaborador del smoke.");

        return ($"CC-{numero}", codigo);
    }

    public static async Task<string> RegistrarSedeAsync(McpClient cliente, CancellationToken ct)
    {
        var codigo = $"TEST-{Guid.CreateVersion7()}";
        var resultado = await cliente.CallToolAsync(
            "registrar_sede",
            new Dictionary<string, object?> { ["codigo"] = codigo, ["nombre"] = "[TEST] Sede MCP" },
            cancellationToken: ct);
        if (resultado.IsError == true)
            throw new InvalidOperationException("No se pudo sembrar la sede del smoke.");

        return codigo;
    }

    public static JsonDocument LeerJson(CallToolResult resultado) =>
        JsonDocument.Parse(resultado.Content.OfType<TextContentBlock>().Single().Text);
}
