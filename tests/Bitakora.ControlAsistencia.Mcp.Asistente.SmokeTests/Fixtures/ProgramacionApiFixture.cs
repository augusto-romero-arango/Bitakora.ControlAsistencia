using Microsoft.Extensions.Configuration;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;

// Atajo de arrange (MEF-ADR-0048 seccion 6), no la cadena que el smoke ejercita: un SEGUNDO
// HttpClient directo al Function App de Programacion, con la MISMA identidad interina que usa el
// servidor MCP (Identidad__TenantIdInterino = tenant-smoke,
// Identidad__OrganizationMembershipIdInterino = om-smoke).
public class ProgramacionApiFixture : IAsyncLifetime
{
    private const string HeaderTenantId = "X-Tenant-Id";
    private const string HeaderUserId = "X-User-Id";
    private const string HeaderOrganizationMembershipId = "X-Organization-Membership-Id";
    private const string TenantId = "tenant-smoke";
    private const string UserId = "smoke@bitakora.dev";
    private const string OrganizationMembershipId = "om-smoke";

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
        Client.DefaultRequestHeaders.Add(HeaderOrganizationMembershipId, OrganizationMembershipId);

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        return ValueTask.CompletedTask;
    }
}
