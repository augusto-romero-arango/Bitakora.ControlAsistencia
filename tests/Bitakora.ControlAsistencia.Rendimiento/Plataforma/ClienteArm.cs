using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;

namespace Bitakora.ControlAsistencia.Rendimiento.Plataforma;

// REST directo contra Azure Resource Manager en lugar de los SDK por servicio: el benchmark solo lee
// (configuracion y metricas), y Azure.Monitor.Query quedo obsoleto en 2025-10. Cada llamada fija su
// api-version, asi que un cambio de SDK no mueve lo que se mide.
public sealed class ClienteArm(TokenCredential credencial, string suscripcion, string grupo) : IDisposable
{
    private static readonly string[] Alcance = ["https://management.azure.com/.default"];

    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri("https://management.azure.com"),
        Timeout = TimeSpan.FromSeconds(60)
    };

    private AccessToken? _token;

    public string IdDeRecurso(string ruta) => $"/subscriptions/{suscripcion}/resourceGroups/{grupo}/providers/{ruta}";

    public Task<JsonElement> GetAsync(string ruta, string apiVersion, CancellationToken ct, string? consulta = null) =>
        EnviarAsync(HttpMethod.Get, $"{IdDeRecurso(ruta)}?api-version={apiVersion}{consulta}", ct);

    public Task<JsonElement> PostAsync(string ruta, string apiVersion, CancellationToken ct) =>
        EnviarAsync(HttpMethod.Post, $"{IdDeRecurso(ruta)}?api-version={apiVersion}", ct);

    public async Task<AccessToken> ObtenerTokenAsync(CancellationToken ct)
    {
        // AzureCliCredential (el az login del operador y el azure/login del workflow) no cachea:
        // cada pedido lanza el CLI. Se reutiliza el token hasta 5 minutos antes de vencer.
        if (_token is { } vigente && vigente.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5))
            return vigente;

        _token = await credencial.GetTokenAsync(new TokenRequestContext(Alcance), ct);
        return _token.Value;
    }

    private async Task<JsonElement> EnviarAsync(HttpMethod metodo, string url, CancellationToken ct)
    {
        var token = await ObtenerTokenAsync(ct);
        using var pedido = new HttpRequestMessage(metodo, url);
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        if (metodo == HttpMethod.Post)
            pedido.Content = new StringContent(string.Empty);

        using var respuesta = await _http.SendAsync(pedido, ct);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
        if (!respuesta.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"ARM respondio {(int)respuesta.StatusCode} para {url.Split('?')[0]}: {Recortar(cuerpo)}");

        using var documento = JsonDocument.Parse(cuerpo);
        return documento.RootElement.Clone();
    }

    private static string Recortar(string texto) => texto.Length <= 300 ? texto : texto[..300] + "...";

    public void Dispose() => _http.Dispose();
}
