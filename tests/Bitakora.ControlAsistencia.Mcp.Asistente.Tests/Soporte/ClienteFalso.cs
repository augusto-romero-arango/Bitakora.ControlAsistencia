using System.Net;
using System.Text;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

/// <summary>
/// HttpMessageHandler falso: responde el JSON enlatado y captura la ultima request (verbo, ruta y body).
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
/// Responde segun una funcion de la request y registra TODAS las requests (<see cref="HandlerEnlatado"/>
/// solo guarda la ultima): para tools que llaman mas de una vez al dominio.
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

/// <summary>
/// Responde por (metodo, ruta) con una fabrica que recibe el cuerpo ya leido, para que un test decida el
/// status inspeccionando el body; sirve a clientes tipados que pegan varias rutas por el MISMO HttpClient.
/// Captura TODAS las requests con lock: la tool llama en paralelo (Parallel.ForEachAsync).
/// </summary>
public sealed class HandlerPorRuta : HttpMessageHandler
{
    private readonly Dictionary<(HttpMethod Metodo, string Ruta), Func<HttpRequestMessage, string?, HttpResponseMessage>> _respuestas = [];

    // Match por prefijo para rutas cuyo tramo final es un id generado DENTRO de la tool (Guid v7 que el
    // test no puede predecir): se recorre en orden de registro y gana el primer prefijo que matchea.
    private readonly List<(HttpMethod Metodo, string Prefijo, Func<HttpRequestMessage, string?, HttpResponseMessage> Fabrica)> _porPrefijo = [];

    public List<(HttpMethod Metodo, string Ruta, string? Cuerpo)> Requests { get; } = [];

    public HandlerPorRuta Responde(HttpMethod metodo, string ruta, HttpStatusCode status, string cuerpo = "") =>
        Responde(metodo, ruta, (_, _) => Respuesta(status, cuerpo));

    public HandlerPorRuta Responde(
        HttpMethod metodo, string ruta, Func<HttpRequestMessage, string?, HttpResponseMessage> respuesta)
    {
        _respuestas[(metodo, ruta)] = respuesta;
        return this;
    }

    public HandlerPorRuta RespondeConPrefijo(
        HttpMethod metodo, string prefijo, HttpStatusCode status, string cuerpo = "") =>
        RespondeConPrefijo(metodo, prefijo, (_, _) => Respuesta(status, cuerpo));

    public HandlerPorRuta RespondeConPrefijo(
        HttpMethod metodo, string prefijo, Func<HttpRequestMessage, string?, HttpResponseMessage> respuesta)
    {
        _porPrefijo.Add((metodo, prefijo, respuesta));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var cuerpo = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        lock (Requests)
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath, cuerpo));

        if (_respuestas.TryGetValue((request.Method, request.RequestUri!.AbsolutePath), out var fabrica))
            return fabrica(request, cuerpo);

        var porPrefijo = _porPrefijo.FirstOrDefault(p =>
            p.Metodo == request.Method && request.RequestUri!.AbsolutePath.StartsWith(p.Prefijo, StringComparison.Ordinal));
        if (porPrefijo.Fabrica is not null)
            return porPrefijo.Fabrica(request, cuerpo);

        throw new InvalidOperationException(
            $"HandlerPorRuta no tiene respuesta registrada para {request.Method} {request.RequestUri.AbsolutePath}");
    }

    private static HttpResponseMessage Respuesta(HttpStatusCode status, string cuerpo) =>
        new(status) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };
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

    public static (HttpClient Cliente, HandlerPorRuta Handler) ConRutas()
    {
        var handler = new HandlerPorRuta();
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
