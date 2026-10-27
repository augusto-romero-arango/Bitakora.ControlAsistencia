using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

/// <summary>
/// Serializador unico de las respuestas de las tools (MEF-ADR-0047 decision 4). Las tools
/// devuelven string para que la forma token-eficiente sea contrato propio, no el ObjectSerializer
/// del worker. El encoder relajado no escapa los caracteres sensibles para HTML (+, &lt;, &gt;, &amp;);
/// es aceptable solo porque la salida es JSON UTF-8 para un cliente MCP: nunca se debe incrustar
/// cruda en HTML.
/// </summary>
public static class RespuestaJson
{
    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Serializar<T>(T valor) => JsonSerializer.Serialize(valor, Opciones);
}
