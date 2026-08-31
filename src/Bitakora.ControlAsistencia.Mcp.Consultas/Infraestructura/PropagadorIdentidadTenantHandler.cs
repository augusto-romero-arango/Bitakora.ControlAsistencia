namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

/// <summary>
/// Estampa X-Tenant-Id/X-User-Id en toda request saliente hacia una Function App del BC. Inocuo en
/// la etapa (a) de tenancy (el TenantResolverFijo de cada dominio no lee estos headers) y
/// obligatorio en la (b) (MEF-ADR-0028 seccion 4).
/// </summary>
public sealed class PropagadorIdentidadTenantHandler(IdentidadTenant identidad) : DelegatingHandler
{
    internal const string HeaderTenantId = "X-Tenant-Id";
    internal const string HeaderUserId = "X-User-Id";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add(HeaderTenantId, identidad.TenantId);
        request.Headers.Add(HeaderUserId, identidad.UserId);
        return base.SendAsync(request, cancellationToken);
    }
}
