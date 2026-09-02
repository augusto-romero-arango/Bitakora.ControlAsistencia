using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.MetadataRecursoProtegido;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests;

// CA-1/CA-2 (issue #576): unit test puro de la derivacion del PRM compartido -- sin red ni host
// (MEF-ADR-0048 seccion 1, nivel 1). La forma que pinnean estas aserciones es la que publica el
// gateway: local.prm_url de infra/modules/apim-mcp-api, {gateway}/well-known/oauth-protected-resource/{path}.
public class UriMetadataRecursoProtegidoTests
{
    [Fact]
    public void ToString_InsertaElSufijoWellKnownEntreElHostYLaRutaDelRecurso()
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

    // El path de una API de APIM admite mas de un segmento, y local.prm_url lo concatena entero
    // detras del sufijo: la ruta completa va despues del well-known, no solo su ultimo segmento.
    [Fact]
    public void ToString_ConservaLaRutaCompleta_CuandoElRecursoTieneVariosSegmentos()
    {
        var prm = new UriMetadataRecursoProtegido(new Uri("https://apim-x.azure-api.net/mcp/consultas"));

        prm.ToString().Should()
            .Be("https://apim-x.azure-api.net/well-known/oauth-protected-resource/mcp/consultas");
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
