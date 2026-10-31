using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Rendimiento.Fixtures;

// Adaptada de Mcp.Asistente.SmokeTests/Fixtures/McpFixture.cs (MEF-ADR-0048): una sesion MCP con la
// key mcp_extension por header. Sin Bearer, el servidor opera con su identidad interina
// (tenant-smoke, mcp-asistente.tf). La diferencia es el HttpClient propio: las tools por grupo
// hacen todos sus POST dentro de una sola llamada y el timeout por defecto (100 s) la cortaria.
public sealed class McpFixture : IAsyncLifetime
{
    public McpClient Cliente { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var baseUrl = Configuracion.Obligatoria("Mcp:BaseUrl", "Es la URL del Function App del servidor MCP.");
        var functionsKey = Configuracion.Obligatoria(
            "Mcp:FunctionsKey",
            "Obtenla con 'az functionapp keys list -g rg-controlasistencias-dev -n func-asist-dev-mcp-asistente --query systemKeys.mcp_extension -o tsv'.");

        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(Configuracion.Leer("Mcp:TimeoutMinutos", 15)) };
        var opciones = new HttpClientTransportOptions
        {
            Endpoint = new Uri(new Uri(baseUrl), "/runtime/webhooks/mcp"),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string> { ["x-functions-key"] = functionsKey }
        };

        Cliente = await McpClient.CreateAsync(new HttpClientTransport(opciones, http, null, ownsHttpClient: true));
    }

    /// <summary>
    /// Texto de la respuesta de la tool. Un rechazo del dominio llega como texto plano (no JSON):
    /// el llamador decide con <see cref="LeerJson"/> si es un resultado o un rechazo.
    /// </summary>
    public async Task<string> LlamarAsync(string tool, Dictionary<string, object?> argumentos, CancellationToken ct)
    {
        var resultado = await Cliente.CallToolAsync(tool, argumentos, cancellationToken: ct);
        var texto = string.Concat(resultado.Content.OfType<TextContentBlock>().Select(b => b.Text));
        if (resultado.IsError == true)
            throw new InvalidOperationException($"La tool {tool} respondio con error: {texto}");

        return texto;
    }

    public static JsonDocument? LeerJson(string texto) =>
        texto.TrimStart().StartsWith('{') ? JsonDocument.Parse(texto) : null;

    public async ValueTask DisposeAsync()
    {
        if (Cliente is not null)
            await Cliente.DisposeAsync();
    }
}
