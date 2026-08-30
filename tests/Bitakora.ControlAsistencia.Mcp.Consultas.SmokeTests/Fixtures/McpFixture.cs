using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.Fixtures;

// Abre UNA sesion MCP contra dev via el SDK oficial de cliente (issue #516): el handshake
// initialize ocurre dentro de McpClient.CreateAsync, asi que si la fixture construye, el host ya
// cargo la extension MCP y respondio con su identidad. La key mcp_extension viaja por header en
// cada request del transporte (AdditionalHeaders); no se versiona -- llega por env
// (Mcp__FunctionsKey) o por appsettings.local.json, nunca por appsettings.json.
public class McpFixture : IAsyncLifetime
{
    public McpClient Cliente { get; private set; } = null!;
    public Uri BaseUrl { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var baseUrl = configuration["Mcp:BaseUrl"]
            ?? throw new InvalidOperationException(
                "Mcp:BaseUrl no esta configurado. Usa appsettings.json, appsettings.local.json o la variable de entorno Mcp__BaseUrl.");

        var functionsKey = configuration["Mcp:FunctionsKey"];
        if (string.IsNullOrWhiteSpace(functionsKey))
            throw new InvalidOperationException(
                "Mcp:FunctionsKey no esta configurada. Obtenla con 'az functionapp keys list' (system key mcp_extension) y pasala por la variable de entorno Mcp__FunctionsKey o por appsettings.local.json.");

        BaseUrl = new Uri(baseUrl);

        Cliente = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(BaseUrl, "/runtime/webhooks/mcp"),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string> { ["x-functions-key"] = functionsKey }
        }));
    }

    public async ValueTask DisposeAsync() => await Cliente.DisposeAsync();
}
