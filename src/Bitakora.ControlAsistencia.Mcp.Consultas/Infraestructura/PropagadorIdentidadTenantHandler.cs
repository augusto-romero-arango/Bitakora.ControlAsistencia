using Bitakora.ControlAsistencia.TenantResolver;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

/// <summary>
/// Estampa X-Tenant-Id/X-User-Id (MEF-ADR-0028 seccion 4) en toda request saliente hacia una
/// Function App del BC. Prefiere la identidad ambiente que <see cref="IdentidadTenantMcpMiddleware"/>
/// deriva del token del usuario autenticado; si ninguna tool call la poblo (llamada directa con
/// system key: smoke, desarrollo local), cae al tenant fijo interino de
/// <see cref="ConfiguracionIdentidadTenant"/>.
/// </summary>
public sealed class PropagadorIdentidadTenantHandler(IdentidadTenant identidad) : DelegatingHandler
{
    internal const string HeaderTenantId = "X-Tenant-Id";
    internal const string HeaderUserId = "X-User-Id";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var (tenantId, userId) = TenantExecutionContext.TryObtener(out var tenantAmbiente, out var userAmbiente)
            ? (tenantAmbiente!, userAmbiente!)
            : (identidad.TenantId, identidad.UserId);

        request.Headers.Add(HeaderTenantId, tenantId);
        request.Headers.Add(HeaderUserId, userId);
        return base.SendAsync(request, cancellationToken);
    }
}
