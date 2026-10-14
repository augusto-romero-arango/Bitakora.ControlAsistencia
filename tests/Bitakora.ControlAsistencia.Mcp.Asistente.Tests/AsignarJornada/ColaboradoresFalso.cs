using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AsignarJornada;

/// <summary>
/// Function App de Colaboradores falso: responde los QUERY (directorio y fichas) con una lista
/// fija y cada PUT colaboradores/{id}/jornada con la fabrica indicada, midiendo cuantos PUTs
/// estan en vuelo a la vez.
/// </summary>
internal sealed class ColaboradoresFalso(
    IReadOnlyList<object> elementos,
    Func<string, HttpResponseMessage>? respuestaPut = null,
    TimeSpan? demoraPut = null) : HttpMessageHandler
{
    private int enVuelo;
    private int maximoEnVuelo;

    public ConcurrentQueue<(string Identificacion, JsonNode Cuerpo)> Puts { get; } = [];
    public ConcurrentQueue<(string Ruta, JsonNode Cuerpo)> Consultas { get; } = [];
    public int MaximoEnVuelo => maximoEnVuelo;
    public IReadOnlyList<string> IdentificacionesConPut => [.. Puts.Select(p => p.Identificacion)];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var ruta = request.RequestUri!.AbsolutePath;
        var cuerpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        if (request.Method.Method == "QUERY")
        {
            Consultas.Enqueue((ruta, JsonNode.Parse(cuerpo!)!));
            return Json(HttpStatusCode.OK, JsonSerializer.Serialize(
                new { elementos }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        }

        if (request.Method == HttpMethod.Put && ruta.EndsWith("/jornada", StringComparison.Ordinal))
        {
            var identificacion = Uri.UnescapeDataString(ruta.Split('/')[^2]);
            Puts.Enqueue((identificacion, JsonNode.Parse(cuerpo!)!));
            var actual = Interlocked.Increment(ref enVuelo);
            int anterior;
            do
            {
                anterior = maximoEnVuelo;
            } while (actual > anterior && Interlocked.CompareExchange(ref maximoEnVuelo, actual, anterior) != anterior);

            try
            {
                if (demoraPut is { } demora)
                    await Task.Delay(demora, cancellationToken);
                return respuestaPut?.Invoke(identificacion) ?? new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            finally
            {
                Interlocked.Decrement(ref enVuelo);
            }
        }

        throw new InvalidOperationException($"Sin respuesta para {request.Method} {ruta}");
    }

    public static HttpResponseMessage Json(HttpStatusCode status, string cuerpo) =>
        new(status) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };

    public static object Entrada(int numero, Guid? jornadaId = null) => new
    {
        identificacion = $"CC-{numero}",
        nombreCompleto = $"Colab {numero}",
        codigoColaborador = $"C{numero}",
        codigoSede = "SUBA",
        vigenteDesde = "2025-01-01",
        vigenteHasta = (string?)null,
        jornadaId
    };

    public static object Ficha(int numero, Guid? jornadaId = null) => new
    {
        id = $"CC-{numero}",
        nombreCompleto = $"Colab {numero}",
        codigoColaborador = $"C{numero}",
        vigenteDesde = "2025-01-01",
        vigenteHasta = (string?)null,
        etiquetas = Array.Empty<object>(),
        codigoSede = "SUBA",
        jornadaId
    };
}
