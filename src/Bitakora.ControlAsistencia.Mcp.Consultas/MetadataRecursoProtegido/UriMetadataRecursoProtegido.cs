namespace Bitakora.ControlAsistencia.Mcp.Consultas.MetadataRecursoProtegido;

// URL absoluta desde la que un cliente MCP sin token lee el documento PRM, y valor que viaja en el
// parametro resource_metadata del WWW-Authenticate. Se deriva del recurso (Mcp__ResourceUri)
// insertando el sufijo well-known entre el host y la ruta, con la barra final ya recortada
// (RFC 9728 s.3.1): asi coincide byte a byte con local.prm_url de infra/modules/apim-mcp-api, que
// es quien publica el documento en el gateway compartido -- un literal paralelo dejaria el
// challenge apuntando a un 404 sin que ningun test lo notara. El sufijo va SIN punto inicial,
// desviacion deliberada del RFC que ya trae el modulo: APIM rechaza con 400 ValidationError un
// path de API que empiece con punto (harness#827).
public sealed partial record UriMetadataRecursoProtegido(Uri Recurso)
{
    private const string SufijoWellKnown = "/well-known/oauth-protected-resource";

    private Uri Valor { get; } = DerivarUrlDelPrm(Recurso);

    public override string ToString() => Valor.ToString();

    private static Uri DerivarUrlDelPrm(Uri recurso)
    {
        var ruta = recurso.AbsolutePath.TrimEnd('/');
        if (ruta.Length == 0)
            throw new ArgumentException(Mensajes.RecursoSinSegmentoDeRuta, nameof(recurso));

        return new Uri($"{recurso.GetLeftPart(UriPartial.Authority)}{SufijoWellKnown}{ruta}");
    }
}
