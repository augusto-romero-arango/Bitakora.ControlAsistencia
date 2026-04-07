using System.Net;
using Microsoft.Extensions.Configuration;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

public class ApiFixture : IAsyncLifetime
{
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

        // Fail-fast: verificar que el entorno esta disponible
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
