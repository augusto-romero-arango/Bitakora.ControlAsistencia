namespace Bitakora.ControlAsistencia.Mcp.Consultas.MetadataRecursoProtegido;

// URL absoluta desde la que un cliente MCP sin token lee el documento PRM, y valor que viaja en el
// parametro resource_metadata del WWW-Authenticate. Se deriva de la misma constante de ruta que
// declara el HttpTrigger: un literal paralelo dejaria el challenge apuntando a un 404 sin que
// ningun test lo notara.
public sealed record UriMetadataRecursoProtegido(Uri Recurso)
{
    private Uri Valor { get; } = new(Recurso, $"/api/{FunctionEndpoint.Ruta}");

    public override string ToString() => Valor.ToString();
}
