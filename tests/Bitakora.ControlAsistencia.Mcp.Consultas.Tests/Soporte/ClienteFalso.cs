using System.Net;
using System.Text;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests.Soporte;

/// <summary>
/// HttpMessageHandler falso (CA-4 del issue #502): responde el JSON enlatado y captura la request
/// para que los tests verifiquen verbo, ruta y body enviados al dominio.
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

/// <summary>
/// HttpMessageHandler falso que responde segun una funcion arbitraria de la request y registra
/// TODAS las requests recibidas (a diferencia de <see cref="HandlerEnlatado"/>, que solo guarda la
/// ultima): necesario para tools que hacen mas de un GET (resolver nombre -> id, issue #629) y
/// deben verificar cuantas veces se llamo al dominio.
/// </summary>
public sealed class HandlerFuncional(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(responder(request));
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

    public static (HttpClient Cliente, HandlerFuncional Handler) ConFuncion(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new HandlerFuncional(responder);
        var cliente = new HttpClient(handler) { BaseAddress = new Uri("https://dominio.falso.local") };
        return (cliente, handler);
    }

    public static HttpResponseMessage JsonOk(string cuerpo) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(cuerpo, Encoding.UTF8, "application/json")
    };

    /// <summary>
    /// Igual que <see cref="Con"/>, pero intercalando <see cref="PropagadorIdentidadTenantHandler"/>
    /// en la cadena -- replica el pipeline real de un HttpClient tipado del servidor.
    /// </summary>
    public static (HttpClient Cliente, HandlerEnlatado Handler) ConIdentidadTenant(
        string json, IdentidadTenant identidad, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new HandlerEnlatado(status, json);
        var propagador = new PropagadorIdentidadTenantHandler(identidad) { InnerHandler = handler };
        var cliente = new HttpClient(propagador) { BaseAddress = new Uri("https://dominio.falso.local") };
        return (cliente, handler);
    }
}
