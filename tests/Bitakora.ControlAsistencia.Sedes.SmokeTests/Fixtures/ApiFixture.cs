using System.Net;
using Microsoft.Extensions.Configuration;

namespace Bitakora.ControlAsistencia.Sedes.SmokeTests.Fixtures;

public class ApiFixture : IAsyncLifetime
{
    // Issue #538: etapa (b) de tenancy (MEF-ADR-0028 seccion 4) exige X-Tenant-Id/X-User-Id en todo
    // request -- TrustedHeadersTenantResolver los lee y lanza si faltan. En la etapa (a) vigente
    // (TenantResolverFijo) declararlos es inocuo, asi que el fixture ya los manda por adelantado.
    private const string TenantIdPorDefecto = "tenant-smoke";
    private const string UserIdPorDefecto = "smoke@bitakora.dev";

    public HttpClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var baseUrl = configuration["Api:BaseUrl"]
            ?? throw new InvalidOperationException(
                "Api:BaseUrl no esta configurado. Usa appsettings.json, appsettings.local.json o la variable de entorno Api__BaseUrl.");

        Client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        Client.DefaultRequestHeaders.Add("X-Tenant-Id", configuration["Tenant:Id"] ?? TenantIdPorDefecto);
        Client.DefaultRequestHeaders.Add("X-User-Id", configuration["Tenant:UserId"] ?? UserIdPorDefecto);

        var response = await Client.GetAsync("/api/health");
        if (response.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException(
                $"El entorno {baseUrl} no esta disponible. Health check retorno {response.StatusCode}.");
    }

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        return ValueTask.CompletedTask;
    }
}
