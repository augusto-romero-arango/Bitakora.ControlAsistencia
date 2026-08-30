using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

/// <summary>
/// Serializador unico de las respuestas de las tools. Las tools devuelven string para que la
/// forma token-eficiente (camelCase, nulls omitidos, acentos sin escapar) sea contrato propio y
/// no dependa del ObjectSerializer del worker.
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
