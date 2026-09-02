namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

/// <summary>
/// DelegatingHandler compartido por todos los HttpClients tipados del servidor (MEF-ADR-0047
/// decision 6): inyecta X-Tenant-Id/X-User-Id en cada request saliente hacia una Function App del
/// BC -- los mismos headers canonicos que TenantContextMiddleware ya sabe leer sin parsing
/// adicional (MEF-ADR-0028 seccion 4). Un unico handler compartido, no un Headers.Add(...)
/// repetido por cliente tipado: ningun HttpClient nuevo puede "olvidar" propagar identidad.
/// </summary>
public sealed class PropagadorIdentidadTenantHandler(IdentidadTenant identidad) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove("X-Tenant-Id");
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", identidad.TenantId);
        request.Headers.Remove("X-User-Id");
        request.Headers.TryAddWithoutValidation("X-User-Id", identidad.UserId);

        return base.SendAsync(request, cancellationToken);
    }
}
