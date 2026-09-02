namespace Bitakora.ControlAsistencia.Mcp.Consultas.MetadataRecursoProtegido;

// URL absoluta desde la que un cliente MCP sin token lee el documento PRM, y valor que viaja en el
// parametro resource_metadata del WWW-Authenticate. Se deriva del layout canonico que publica el
// gateway compartido (RFC 9728 s.3.1, apim-gateway-scaffolder 0.35.0):
// {gateway}/well-known/oauth-protected-resource/{path}, mismo {gateway} y {path} del recurso -- un
// literal paralelo dejaria el challenge apuntando a un 404 sin que ningun test lo notara.
public sealed partial record UriMetadataRecursoProtegido(Uri Recurso)
{
    private Uri Valor { get; } = DerivarUrlDelPrm(Recurso);

    public override string ToString() => Valor.ToString();

    private static Uri DerivarUrlDelPrm(Uri recurso)
    {
        var segmentos = recurso.Segments;
        if (segmentos.Length < 2)
            throw new ArgumentException(Mensajes.RecursoSinSegmentoDeRuta, nameof(recurso));

        var ultimoSegmento = segmentos[^1].TrimEnd('/');
        var rutaBase = string.Concat(segmentos[..^1]);
        var authority = recurso.GetLeftPart(UriPartial.Authority);

        return new Uri($"{authority}{rutaBase}well-known/oauth-protected-resource/{ultimoSegmento}");
    }
}
