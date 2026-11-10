using Bitakora.ControlAsistencia.TenantResolver;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

/// <summary>
/// Unico DelegatingHandler compartido por todos los HttpClients tipados, en vez de un Headers.Add por
/// cliente: ningun HttpClient nuevo puede olvidar propagar identidad. Inyecta los headers que lee
/// TenantContextMiddleware. Prefiere la identidad ambiente que <see cref="IdentidadTenantMcpMiddleware"/>
/// deriva del token; sin ella (llamada directa con system key: smoke, desarrollo local) cae al tenant
/// fijo recibido por constructor.
/// </summary>
public sealed class PropagadorIdentidadTenantHandler(IdentidadTenant identidad) : DelegatingHandler
{
    internal const string HeaderTenantId = "X-Tenant-Id";
    internal const string HeaderUserId = "X-User-Id";
    internal const string HeaderOrganizationMembershipId = "X-Organization-Membership-Id";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var (tenantId, userId) = TenantExecutionContext.TryObtener(out var tenantAmbiente, out var userAmbiente)
            ? (tenantAmbiente!, userAmbiente!)
            : (identidad.TenantId, identidad.UserId);
        var membershipId = TenantExecutionContext.TryObtenerMembershipId(out var membershipAmbiente)
            ? membershipAmbiente!
            : identidad.OrganizationMembershipId;

        // Remove antes de agregar: el pipeline de HttpClientFactory reusa el HttpRequestMessage en
        // un reintento, y un header repetido llegaria al BC como dos valores.
        request.Headers.Remove(HeaderTenantId);
        request.Headers.TryAddWithoutValidation(HeaderTenantId, tenantId);
        request.Headers.Remove(HeaderUserId);
        request.Headers.TryAddWithoutValidation(HeaderUserId, userId);
        request.Headers.Remove(HeaderOrganizationMembershipId);
        request.Headers.TryAddWithoutValidation(HeaderOrganizationMembershipId, membershipId);

        return base.SendAsync(request, cancellationToken);
    }
}
