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

/// <summary>
/// HttpMessageHandler falso que responde por verbo+ruta: HandlerEnlatado alcanza cuando un cliente
/// tipado solo pega una ruta, pero ProgramacionApi responde dos (GET turnos, POST solicitudes) por
/// el MISMO HttpClient -- y el POST se llama N veces con outcomes distintos (uno 202, otro 409).
/// Registra una fabrica de respuesta por (metodo, ruta) que recibe el cuerpo ya leido, para que un
/// test pueda decidir el status inspeccionando el body (p.ej. por identificacion del colaborador).
/// Captura TODAS las requests con lock: la tool llama en paralelo (Parallel.ForEachAsync).
/// </summary>
public sealed class HandlerPorRuta : HttpMessageHandler
{
    private readonly Dictionary<(HttpMethod Metodo, string Ruta), Func<HttpRequestMessage, string?, HttpResponseMessage>> _respuestas = [];

    // Match por prefijo para rutas cuyo tramo final lo decide un id generado DENTRO de la tool
    // (p.ej. el plantillaId de crear_plantilla_semanal, Guid v7 que el test no puede predecir antes
    // de ejecutar el Run): a diferencia de _respuestas (ruta exacta), esta lista se recorre en
    // orden de registro y usa el primer prefijo que matchea metodo + StartsWith.
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

    public static (HttpClient Cliente, HandlerPorRuta Handler) ConRutas()
    {
        var handler = new HandlerPorRuta();
        var cliente = new HttpClient(handler) { BaseAddress = new Uri("https://dominio.falso.local") };
        return (cliente, handler);
    }
}
