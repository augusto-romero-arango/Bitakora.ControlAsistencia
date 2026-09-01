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
    private static readonly Uri AuthorizationServer =
        new("https://api.workos.com/user_management/client_01M1CKPECJ5DBRMS3ZVFRQW8GW");

    [Fact]
    public void Construir_DeclaraElRecursoYAuthKitComoUnicoAuthorizationServer()
    {
        var constructor = new ConstructorMetadataRecursoProtegido(Recurso, AuthorizationServer);

        var documento = constructor.Construir();

        documento.Resource.Should().Be(Recurso.ToString());
        documento.AuthorizationServers.Should().ContainSingle().Which.Should().Be(AuthorizationServer.ToString());
    }

    [Fact]
    public void Construir_SerializaConLosNombresDePropiedadDeLaSpecRfc9728()
    {
        var documento = new ConstructorMetadataRecursoProtegido(Recurso, AuthorizationServer).Construir();

        var json = JsonSerializer.Serialize(documento);

        json.Should().Contain("\"resource\":").And.Contain("\"authorization_servers\":");
    }
}
