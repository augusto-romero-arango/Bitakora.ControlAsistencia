using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.MetadataRecursoProtegido;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests;

// CA-1/CA-2 (issue #576): unit test puro de la derivacion del PRM compartido -- sin red ni host
// (MEF-ADR-0048 seccion 1, nivel 1). El layout canonico es el de apim-gateway-scaffolder 0.35.0:
// {gateway}/well-known/oauth-protected-resource/{path}, mismo {gateway} y {path} del recurso.
public class UriMetadataRecursoProtegidoTests
{
    [Fact]
    public void ToString_DerivaLaUrlDelPrmCompartido_DesdeElUltimoSegmentoDeRuta()
    {
        var prm = new UriMetadataRecursoProtegido(new Uri("https://apim-x.azure-api.net/mcp-consultas"));

        prm.ToString().Should()
            .Be("https://apim-x.azure-api.net/well-known/oauth-protected-resource/mcp-consultas");
    }

    [Fact]
    public void ToString_ProduceLaMismaUrl_CuandoElRecursoTerminaConBarra()
    {
        var prm = new UriMetadataRecursoProtegido(new Uri("https://apim-x.azure-api.net/mcp-consultas/"));

        prm.ToString().Should()
            .Be("https://apim-x.azure-api.net/well-known/oauth-protected-resource/mcp-consultas");
    }

    [Fact]
    public void Constructor_LanzaArgumentException_CuandoElRecursoNoTieneSegmentoDeRuta()
    {
        var act = () => new UriMetadataRecursoProtegido(new Uri("https://apim-x.azure-api.net"));

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{UriMetadataRecursoProtegido.Mensajes.RecursoSinSegmentoDeRuta}*");
    }

    [Fact]
    public void Constructor_LanzaArgumentException_CuandoElRecursoEsSoloElAuthorityConBarraFinal()
    {
        var act = () => new UriMetadataRecursoProtegido(new Uri("https://apim-x.azure-api.net/"));

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{UriMetadataRecursoProtegido.Mensajes.RecursoSinSegmentoDeRuta}*");
    }
}
