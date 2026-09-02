using System.Net;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Bitakora.ControlAsistencia.TenantResolver;

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

    // CA-1/CA-3 (issue #572): IdentidadTenantMcpMiddleware puebla el ambiente cuando la tool call
    // trae un Bearer con org_id/sub; el propagador debe preferir esa identidad sobre el tenant fijo
    // interino inyectado por constructor. El fallback al tenant fijo (sin Bearer: smoke, local) lo
    // cubre el test de arriba, que no puebla el ambiente.
    [Fact]
    public async Task Send_PrefiereLaIdentidadAmbiente_CuandoElMiddlewareMcpLaPoblo()
    {
        TenantExecutionContext.SetDerivedIdentity("org_acme", "usuario_123");
        HttpRequestMessage? requestCapturado = null;
        var handler = new PropagadorIdentidadTenantHandler(new IdentidadTenant("tenant-fijo-interino", "mcp-sin-usuario-autenticado"))
        {
            InnerHandler = new HandlerCapturador(r => requestCapturado = r)
        };
        var cliente = new HttpClient(handler) { BaseAddress = new Uri("https://dominio.falso.local") };

        await cliente.GetAsync("api/recurso", TestContext.Current.CancellationToken);

        requestCapturado.Should().NotBeNull();
        requestCapturado!.Headers.GetValues("X-Tenant-Id").Should().ContainSingle().Which.Should().Be("org_acme");
        requestCapturado.Headers.GetValues("X-User-Id").Should().ContainSingle().Which.Should().Be("usuario_123");
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
