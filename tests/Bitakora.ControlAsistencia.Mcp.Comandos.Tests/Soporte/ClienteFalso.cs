using System.Net;
using System.Text;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Soporte;

/// <summary>
/// HttpMessageHandler falso: responde el cuerpo enlatado y captura la request para que los tests
/// verifiquen verbo, ruta y body enviados al dominio -- o que no hubo request alguna.
/// </summary>
public sealed class HandlerEnlatado(HttpStatusCode status, string cuerpo) : HttpMessageHandler
{
    public HttpRequestMessage? UltimaRequest { get; private set; }
    public string? UltimoCuerpoEnviado { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        UltimaRequest = request;
        UltimoCuerpoEnviado = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        return new HttpResponseMessage(status)
        {
            Content = new StringContent(cuerpo, Encoding.UTF8, "application/json")
        };
    }
}

public static class ClienteFalso
{
    public static (HttpClient Cliente, HandlerEnlatado Handler) Con(
        string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new HandlerEnlatado(status, json);
        var cliente = new HttpClient(handler) { BaseAddress = new Uri("https://dominio.falso.local") };
        return (cliente, handler);
    }
}
