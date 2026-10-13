using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;

// Hermano de ProgramacionApiFixture (misma excepcion documentada, MEF-ADR-0048 seccion 6): asignar
// la Jornada a un colaborador todavia no tiene tool, asi que el arrange llama directo al Function App
// de Colaboradores con la MISMA identidad interina que usa el servidor MCP en sus HttpClients
// tipados. Es un atajo de arrange, no la cadena que el smoke test ejercita.
public class ColaboradoresApiFixture : IAsyncLifetime
{
    private const string HeaderTenantId = "X-Tenant-Id";
    private const string HeaderUserId = "X-User-Id";
    private const string HeaderOrganizationMembershipId = "X-Organization-Membership-Id";
    private const string TenantId = "tenant-smoke";
    private const string UserId = "smoke@bitakora.dev";
    private const string OrganizationMembershipId = "om-smoke";

    private static readonly HttpMethod Query = new("QUERY");

    public HttpClient Client { get; private set; } = null!;

    public ValueTask InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var baseUrl = configuration["Api:Colaboradores:BaseUrl"]
            ?? throw new InvalidOperationException(
                "Api:Colaboradores:BaseUrl no esta configurado. Usa appsettings.json, appsettings.local.json o la variable de entorno Api__Colaboradores__BaseUrl.");

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

    public async Task AsignarJornadaAsync(string identificacion, Guid jornadaId, CancellationToken ct)
    {
        var respuesta = await Client.PutAsJsonAsync(
            $"/api/colaboradores/{Uri.EscapeDataString(identificacion)}/jornada", new { jornadaId }, ct);
        if (respuesta.StatusCode != HttpStatusCode.NoContent)
            throw new InvalidOperationException(
                $"No se pudo asignar la Jornada al colaborador del smoke: {(int)respuesta.StatusCode}.");
    }

    /// <summary>El jornadaId que el directorio devuelve para esa identificacion, o null si aun no lo refleja.</summary>
    public async Task<string?> JornadaEnDirectorioAsync(string identificacion, CancellationToken ct)
    {
        var request = new HttpRequestMessage(Query, "/api/colaboradores/directorio")
        {
            Content = JsonContent.Create(new { identificaciones = new[] { identificacion } })
        };
        var respuesta = await Client.SendAsync(request, ct);
        respuesta.EnsureSuccessStatusCode();

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(ct));
        return documento.RootElement.EnumerateArray()
            .Where(e => e.GetProperty("identificacion").GetString() == identificacion)
            .Select(e => e.TryGetProperty("jornadaId", out var jornada) ? jornada.GetString() : null)
            .FirstOrDefault();
    }
}
