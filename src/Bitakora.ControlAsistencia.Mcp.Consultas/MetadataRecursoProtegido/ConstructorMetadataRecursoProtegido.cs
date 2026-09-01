namespace Bitakora.ControlAsistencia.Mcp.Consultas.MetadataRecursoProtegido;

public sealed class ConstructorMetadataRecursoProtegido(Uri recurso, Uri authorizationServer)
{
    public DocumentoRecursoProtegido Construir() =>
        new(recurso.ToString(), [authorizationServer.ToString()]);
}
