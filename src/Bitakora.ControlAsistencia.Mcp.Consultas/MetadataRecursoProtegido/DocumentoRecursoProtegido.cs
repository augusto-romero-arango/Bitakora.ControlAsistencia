using System.Text.Json.Serialization;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.MetadataRecursoProtegido;

// Forma fijada por RFC 9728 (OAuth 2.0 Protected Resource Metadata) y la guia WorkOS MCP: nombres
// de propiedad en snake_case porque son el vocabulario fijo de la spec -- no el contrato
// token-eficiente camelCase de RespuestaJson (que aplica a las respuestas de tools, no a este
// documento de metadata).
public sealed record DocumentoRecursoProtegido(
    [property: JsonPropertyName("resource")] string Resource,
    [property: JsonPropertyName("authorization_servers")] IReadOnlyList<string> AuthorizationServers);
