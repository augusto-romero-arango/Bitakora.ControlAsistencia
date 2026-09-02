using System.Net;
using System.Text;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Soporte;

public sealed class HandlerEnlatado(HttpStatusCode status, string cuerpo) : HttpMessageHandler
{
    public HttpRequestMessage? UltimaSolicitud { get; private set; }
    public string? UltimoBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        UltimaSolicitud = request;
        UltimoBody = request.Content is null
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
    public static HttpClient Con(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        Con(json, status, out _);

    public static HttpClient Con(string json, HttpStatusCode status, out HandlerEnlatado handler)
    {
        handler = new HandlerEnlatado(status, json);
        return new HttpClient(handler) { BaseAddress = new Uri("https://dominio.falso.local") };
    }
}
