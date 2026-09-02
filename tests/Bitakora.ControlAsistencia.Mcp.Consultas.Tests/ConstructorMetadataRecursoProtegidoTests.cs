using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.MetadataRecursoProtegido;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests;

// CA-3/CA-4: shape del documento PRM (RFC 9728). Los nombres de propiedad snake_case son
// contrato de la spec, no del serializador token-eficiente de las tools -- por eso el segundo test
// pinnea el JSON crudo en vez de solo comparar el record.
public class ConstructorMetadataRecursoProtegidoTests
{
    private static readonly Uri Recurso = new("https://mcp-consultas.controlasistencia.example.com");
    // Dominio AuthKit del entorno, no el issuer de LOGIN del gateway (issue #560).
    private const string DominioAuthKit = "https://marvelous-polaroid-97-staging.authkit.app";
    private static readonly Uri AuthorizationServer = new(DominioAuthKit);

    [Fact]
    public void Construir_DeclaraElRecursoYAuthKitComoUnicoAuthorizationServer()
    {
        var constructor = new ConstructorMetadataRecursoProtegido(Recurso, AuthorizationServer);

        var documento = constructor.Construir();

        documento.Resource.Should().Be(Recurso.OriginalString);
        documento.AuthorizationServers.Should().ContainSingle().Which.Should().Be(DominioAuthKit);
    }

    // El dominio AuthKit no tiene path: Uri lo normalizaria a ".../" y el issuer dejaria de
    // coincidir byte a byte con el del discovery doc (RFC 8414).
    [Fact]
    public void Construir_EmiteElAuthorizationServerSinBarraFinal_CuandoEsUnDominioSinPath()
    {
        var documento = new ConstructorMetadataRecursoProtegido(Recurso, AuthorizationServer).Construir();

        documento.AuthorizationServers.Should().ContainSingle()
            .Which.Should().Be(DominioAuthKit).And.NotEndWith("/");
    }

    [Fact]
    public void Construir_SerializaConLosNombresDePropiedadDeLaSpecRfc9728()
    {
        var documento = new ConstructorMetadataRecursoProtegido(Recurso, AuthorizationServer).Construir();

        var json = JsonSerializer.Serialize(documento);

        json.Should().Contain("\"resource\":").And.Contain("\"authorization_servers\":");
    }
}
