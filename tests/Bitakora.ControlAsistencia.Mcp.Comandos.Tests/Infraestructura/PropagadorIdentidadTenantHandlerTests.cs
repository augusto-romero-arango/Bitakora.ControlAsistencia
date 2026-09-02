using System.Net;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Infraestructura;

public class PropagadorIdentidadTenantHandlerTests
{
    [Fact]
    public async Task Send_PropagaTenantIdYUserId_EnCadaRequestSaliente()
    {
        HttpRequestMessage? requestCapturado = null;
        var handler = new PropagadorIdentidadTenantHandler(new IdentidadTenant("tenant-123", "usuario-456"))
        {
            InnerHandler = new HandlerCapturador(r => requestCapturado = r)
        };
        var cliente = new HttpClient(handler) { BaseAddress = new Uri("https://dominio.falso.local") };

        await cliente.GetAsync("api/recurso", TestContext.Current.CancellationToken);

        requestCapturado.Should().NotBeNull();
        requestCapturado!.Headers.GetValues("X-Tenant-Id").Should().ContainSingle().Which.Should().Be("tenant-123");
        requestCapturado.Headers.GetValues("X-User-Id").Should().ContainSingle().Which.Should().Be("usuario-456");
    }
}

internal sealed class HandlerCapturador(Action<HttpRequestMessage> capturar) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        capturar(request);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
