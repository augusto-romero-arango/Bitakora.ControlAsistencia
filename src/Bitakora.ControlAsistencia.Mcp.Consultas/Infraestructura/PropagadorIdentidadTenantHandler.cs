namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

/// <summary>
/// DelegatingHandler compartido entre los HttpClients tipados del servidor: agrega
/// X-Tenant-Id/X-User-Id a toda request saliente hacia una Function App del BC.
/// Forward-compatible con la etapa (b) de tenancy (MEF-ADR-0028 seccion 4) -- en etapa (a) el
/// TenantResolverFijo de cada dominio no lee estos headers, asi que enviarlos hoy es inocuo.
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
