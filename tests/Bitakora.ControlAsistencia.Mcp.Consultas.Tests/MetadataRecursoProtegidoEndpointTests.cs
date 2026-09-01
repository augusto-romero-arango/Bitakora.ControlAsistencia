using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.MetadataRecursoProtegido;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests;

// CA-3: el documento PRM se sirve anonimamente (sin Bearer en el request de prueba, replica el
// acceso de un cliente MCP que todavia no tiene token).
public class MetadataRecursoProtegidoEndpointTests
{
    private static readonly Uri Recurso = new("https://mcp-consultas.controlasistencia.example.com");
    private static readonly Uri AuthorizationServer =
        new("https://api.workos.com/user_management/client_01M1CKPECJ5DBRMS3ZVFRQW8GW");

    private static HttpRequest FakeHttpRequestAnonima() => new DefaultHttpContext().Request;

    [Fact]
    public void Run_Retorna200ConElDocumentoDeMetadata_CuandoSeConsultaAnonimamente()
    {
        var endpoint = new FunctionEndpoint(new ConstructorMetadataRecursoProtegido(Recurso, AuthorizationServer));

        var resultado = endpoint.Run(FakeHttpRequestAnonima());

        var objectResult = resultado.Should().BeOfType<OkObjectResult>().Which;
        var documento = objectResult.Value.Should().BeOfType<DocumentoRecursoProtegido>().Which;
        documento.Resource.Should().Be(Recurso.ToString());
        documento.AuthorizationServers.Should().ContainSingle().Which.Should().Be(AuthorizationServer.ToString());
    }
}
