using Cosmos.MultiTenancy;

namespace Bitakora.ControlAsistencia.Programacion.Infraestructura;

// Issue #219: ITenantResolver mono-tenant. Cosmos.Event* 2.x dejo de auto-registrar un
// ITenantResolver por defecto en AgregarWolverineCommandRouter, pero WolverineCommandRouter/
// WolverineQueryRouter/WolverinePublicEventSender/WolverinePrivateEventSender lo siguen
// exigiendo por constructor.
// Este proyecto es mono-tenant: se registra un ITenantResolver con valores fijos en vez de los
// resolvers header-based de 2.x (AgregarTenantResolverHibrido/ProxyTenantResolver), que exigirian
// headers TenantId/user_id inexistentes en este flujo HTTP.
// Ver docs/adr/00XX-estrategia-tenancy-mono-tenant.md (numero pendiente de asignar por el
// implementer) y docs/bitacora/field-notes/2026-07-18-1905-bug-investigation.md.
public sealed class TenantResolverFijo : ITenantResolver
{
    public string TenantId => throw new NotImplementedException();

    public string UserId => throw new NotImplementedException();
}
