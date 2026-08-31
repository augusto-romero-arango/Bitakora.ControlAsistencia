using Cosmos.MultiTenancy;
using JasperFx;

namespace Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

// TODO(tenancy etapa b): resolver mono-tenant transitorio en la forma canonica de MEF-ADR-0028
// seccion 2. La infraestructura es multi-tenant conjoined (CA-ADR-0027) pero opera con un unico
// tenant logico; los resolvers header-based de Cosmos.MultiTenancy 2.x exigirian headers que este
// flujo HTTP no envia. Al instalar autenticacion con TenantContext (/install-auth), reemplazar por
// el resolver real de la etapa (b).
public sealed class TenantResolverMonoTenantPorDefecto : ITenantResolver
{
    public string TenantId => StorageConstants.DefaultTenantId;

    public string UserId => "usuario-no-autenticado";
}
