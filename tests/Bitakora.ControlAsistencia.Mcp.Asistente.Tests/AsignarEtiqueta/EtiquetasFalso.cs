using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AsignarEtiqueta;

/// <summary>
/// Function App de Colaboradores falso: responde el QUERY del directorio con una lista fija y
/// registra cada PUT/DELETE colaboradores/{id}/etiquetas/{categoria} con la ruta tal como viajo.
/// </summary>
internal sealed class EtiquetasFalso(
    IReadOnlyList<object> elementos,
    Func<string, HttpResponseMessage>? respuestaEscritura = null,
    bool directorioFalla = false) : HttpMessageHandler
{
    public ConcurrentQueue<(string Metodo, string Ruta, string Identificacion, JsonNode? Cuerpo)> Escrituras { get; } = [];
    public ConcurrentQueue<JsonNode> Consultas { get; } = [];
    public IReadOnlyList<string> IdentificacionesEscritas => [.. Escrituras.Select(e => e.Identificacion)];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var ruta = request.RequestUri!.AbsolutePath;
        var cuerpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        if (request.Method.Method == "QUERY")
        {
            Consultas.Enqueue(JsonNode.Parse(cuerpo!)!);
            return directorioFalla
                ? Json(HttpStatusCode.InternalServerError, "directorio caido")
                : Json(HttpStatusCode.OK, JsonSerializer.Serialize(
                    new { elementos }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        }

        if ((request.Method == HttpMethod.Put || request.Method == HttpMethod.Delete)
            && ruta.Contains("/etiquetas/", StringComparison.Ordinal))
        {
            var identificacion = Uri.UnescapeDataString(ruta.Split('/')[^3]);
            Escrituras.Enqueue((request.Method.Method, ruta, identificacion, cuerpo is null ? null : JsonNode.Parse(cuerpo)));
            return respuestaEscritura?.Invoke(identificacion) ?? new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        throw new InvalidOperationException($"Sin respuesta para {request.Method} {ruta}");
    }

    public static HttpResponseMessage Json(HttpStatusCode status, string cuerpo) =>
        new(status) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };
}
