using System.Net;
using Microsoft.Extensions.Configuration;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;

public class ApiFixture : IAsyncLifetime
{
    // Nombres canonicos que lee TrustedHeadersTenantResolver, fijados por decompilacion de
    // Cosmos.MultiTenancy.AspNetCore en MEF-ADR-0028. Los valores salen de IdentidadDePrueba.
    private const string HeaderTenantId = "X-Tenant-Id";
    private const string HeaderUserId = "X-User-Id";

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
        var identidad = IdentidadDePrueba.Desde(configuration);
        Client.DefaultRequestHeaders.Add(HeaderTenantId, identidad.TenantId);
        Client.DefaultRequestHeaders.Add(HeaderUserId, identidad.UserId);

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
