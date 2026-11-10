using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

/// <summary>
/// Las tools devuelven string para que la forma token-eficiente sea contrato propio y no la del
/// ObjectSerializer del worker. El encoder relajado no escapa los caracteres sensibles para HTML:
/// aceptable solo porque la salida es JSON para un cliente MCP; nunca incrustarla cruda en HTML.
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
