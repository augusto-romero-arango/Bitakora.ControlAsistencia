using Microsoft.Extensions.Configuration;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.Fixtures;

// Excepcion documentada (MEF-ADR-0048 seccion 6): el arrange del smoke de solicitar_programacion_turno
// necesita sembrar un turno directo en Programacion -- esta tool no tiene create-turno -- asi que
// este fixture abre un SEGUNDO HttpClient, directo al Function App de Programacion, con la MISMA
// identidad interina que usa el propio servidor de Comandos para sus HttpClients tipados
// (Identidad__TenantIdInterino = tenant-smoke, issue #572). No pasa por el MCP: es un atajo de
// arrange, no la cadena que el smoke test ejercita.
public class ProgramacionApiFixture : IAsyncLifetime
{
    private const string HeaderTenantId = "X-Tenant-Id";
    private const string HeaderUserId = "X-User-Id";
    private const string TenantId = "tenant-smoke";
    private const string UserId = "smoke@bitakora.dev";

    public HttpClient Client { get; private set; } = null!;

    public ValueTask InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var baseUrl = configuration["Api:Programacion:BaseUrl"]
            ?? throw new InvalidOperationException(
                "Api:Programacion:BaseUrl no esta configurado. Usa appsettings.json, appsettings.local.json o la variable de entorno Api__Programacion__BaseUrl.");

        Client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        Client.DefaultRequestHeaders.Add(HeaderTenantId, TenantId);
        Client.DefaultRequestHeaders.Add(HeaderUserId, UserId);

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        return ValueTask.CompletedTask;
    }
}
