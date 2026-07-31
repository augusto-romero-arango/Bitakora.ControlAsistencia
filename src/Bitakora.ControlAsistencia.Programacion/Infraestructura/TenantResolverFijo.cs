using Cosmos.MultiTenancy;
using JasperFx;

namespace Bitakora.ControlAsistencia.Programacion.Infraestructura;

// Issue #219: ITenantResolver de tenant unico. Cosmos.Event* 2.x dejo de auto-registrar un
// ITenantResolver por defecto en AgregarWolverineCommandRouter, pero WolverineCommandRouter/
// WolverineQueryRouter/WolverinePublicEventSender/WolverinePrivateEventSender lo siguen
// exigiendo por constructor.
// La infraestructura de este proyecto es multi-tenant conjoined (CA-ADR-0027) pero opera con un
// unico tenant logico: se registra un ITenantResolver con valores fijos en vez de los resolvers
// header-based de 2.x (AgregarTenantResolverHibrido/ProxyTenantResolver), que exigirian headers
// TenantId/user_id inexistentes en este flujo HTTP.
// Ver docs/adr/ca-adr-0027-tenancy-conjoined-con-tenant-unico.md y
// docs/bitacora/field-notes/2026-07-18-1905-bug-investigation.md.
public sealed class TenantResolverFijo : ITenantResolver
{
    private const string TenantIdFijo = StorageConstants.DefaultTenantId;
    private const string UserIdFijo = "sin-identificar";

    public string TenantId => TenantIdFijo;

    public string UserId => UserIdFijo;
}
