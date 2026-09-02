using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

/// <summary>
/// Serializador unico de las respuestas de las tools (MEF-ADR-0047 decision 4). Las tools
/// devuelven string para que la forma token-eficiente sea contrato propio, no el ObjectSerializer
/// del worker.
/// </summary>
public static class RespuestaJson
{
    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public static string Serializar<T>(T valor) => JsonSerializer.Serialize(valor, Opciones);
}
