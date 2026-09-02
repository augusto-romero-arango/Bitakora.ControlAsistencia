using System.Net;
using System.Text;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Ejemplo.Soporte;

public sealed class HandlerEnlatado(HttpStatusCode status, string cuerpo) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(cuerpo, Encoding.UTF8, "application/json")
        });
}

public static class ClienteFalso
{
    public static HttpClient Con(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(new HandlerEnlatado(status, json)) { BaseAddress = new Uri("https://dominio.falso.local") };
}
