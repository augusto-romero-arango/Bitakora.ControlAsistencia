namespace Bitakora.ControlAsistencia.Mcp.Consultas.MetadataRecursoProtegido;

// OriginalString y no ToString(): Uri normaliza un authority sin path anadiendole "/", y tanto el
// issuer del authorization server (RFC 8414) como el resource (Resource Indicator registrado en
// WorkOS) deben viajar byte a byte como fueron declarados -- una barra de mas y el cliente MCP no
// reconoce el authorization server que acaba de descubrir (issue #560).
public sealed class ConstructorMetadataRecursoProtegido(Uri recurso, Uri authorizationServer)
{
    public DocumentoRecursoProtegido Construir() =>
        new(recurso.OriginalString, [authorizationServer.OriginalString]);
}
