using Bitakora.ControlAsistencia.TenantResolver;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

/// <summary>
/// DelegatingHandler compartido por todos los HttpClients tipados del servidor (MEF-ADR-0047
/// decision 6): inyecta X-Tenant-Id/X-User-Id en cada request saliente hacia una Function App del
/// BC -- los mismos headers canonicos que TenantContextMiddleware ya sabe leer sin parsing
/// adicional (MEF-ADR-0028 seccion 4). Un unico handler compartido, no un Headers.Add(...)
/// repetido por cliente tipado: ningun HttpClient nuevo puede "olvidar" propagar identidad.
/// Prefiere la identidad ambiente que <see cref="IdentidadTenantMcpMiddleware"/> deriva del token
/// del usuario autenticado; si ninguna tool call la poblo (llamada directa con system key: smoke,
/// desarrollo local), cae al tenant fijo interino recibido por constructor.
/// </summary>
public sealed class PropagadorIdentidadTenantHandler(IdentidadTenant identidad) : DelegatingHandler
{
    internal const string HeaderTenantId = "X-Tenant-Id";
    internal const string HeaderUserId = "X-User-Id";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var (tenantId, userId) = TenantExecutionContext.TryObtener(out var tenantAmbiente, out var userAmbiente)
            ? (tenantAmbiente!, userAmbiente!)
            : (identidad.TenantId, identidad.UserId);

        // Remove antes de agregar: el pipeline de HttpClientFactory reusa el HttpRequestMessage en
        // un reintento, y un header repetido llegaria al BC como dos valores.
        request.Headers.Remove(HeaderTenantId);
        request.Headers.TryAddWithoutValidation(HeaderTenantId, tenantId);
        request.Headers.Remove(HeaderUserId);
        request.Headers.TryAddWithoutValidation(HeaderUserId, userId);

        return base.SendAsync(request, cancellationToken);
    }
}
