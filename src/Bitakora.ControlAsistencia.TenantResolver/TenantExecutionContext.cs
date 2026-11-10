using Cosmos.MultiTenancy;

namespace Bitakora.ControlAsistencia.TenantResolver;

/// <summary>
/// Identidad (tenant + usuario + membership de organizacion) de la invocacion en curso, poblada por
/// <see cref="TenantContextMiddleware"/>.
///
/// El estado vive en <see cref="AsyncLocal{T}"/> y no en la instancia a proposito (CA-ADR-0032):
/// Wolverine resuelve sus handlers en su PROPIO <c>IServiceScope</c> hijo del contenedor raiz, distinto
/// del scope de la invocacion que puebla el middleware, asi que un holder con estado por instancia
/// llegaria vacio al handler. El AsyncLocal fluye por la cadena async sin depender del scope de DI.
///
/// Los getters de <see cref="ITenantContext"/> lanzan si la identidad no se resolvio, para que un fallo
/// del gateway o un mensaje sin identidad no pase desapercibido.
/// </summary>
public sealed class TenantExecutionContext : ITenantContext
{
    private static readonly AsyncLocal<string?> _tenantId = new();
    private static readonly AsyncLocal<string?> _userId = new();
    private static readonly AsyncLocal<string?> _organizationMembershipId = new();

    internal static void Set(string? tenantId, string? userId, string? organizationMembershipId = null)
    {
        _tenantId.Value = tenantId;
        _userId.Value = userId;
        _organizationMembershipId.Value = organizationMembershipId;
    }

    /// <summary>
    /// Puebla la identidad cuando el llamador no puede presentar un JWT (webhooks de un proveedor
    /// externo, timers). <paramref name="tenantId"/> es el tenant de EJECUCION, el que decide la
    /// particion de Marten; <paramref name="actor"/> nombra al proceso que escribe, porque no hay usuario.
    /// Ambos son obligatorios porque el sender lee TenantId y UserId en cada publish.
    ///
    /// No usar desde Functions HTTP detras del gateway: sobreescribiria la identidad que puebla el
    /// middleware.
    /// </summary>
    public static void SetDerivedIdentity(string tenantId, string actor, string? organizationMembershipId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);

        Set(tenantId, actor, organizationMembershipId);
    }

    public string TenantId => AssertValue(_tenantId.Value, "tenant", "X-Tenant-Id", "tenant-id");

    public string UserId => AssertValue(_userId.Value, "usuario", "X-User-Id", "user_id");

    public string OrganizationMembershipId => AssertValue(
        _organizationMembershipId.Value, "membership de organización",
        "X-Organization-Membership-Id", "organization_membership_id");

    /// <summary>
    /// Version sin lanzar de los getters. Retorna <c>false</c> si cualquiera de los dos valores no esta
    /// poblado, y entonces ambos out quedan en <c>null</c>, nunca uno solo.
    /// </summary>
    public static bool TryObtener(out string? tenantId, out string? userId)
    {
        if (string.IsNullOrWhiteSpace(_tenantId.Value) || string.IsNullOrWhiteSpace(_userId.Value))
        {
            tenantId = null;
            userId = null;
            return false;
        }

        tenantId = _tenantId.Value;
        userId = _userId.Value;
        return true;
    }

    /// <summary>
    /// Version sin lanzar de <see cref="OrganizationMembershipId"/>. <see cref="Set"/> siempre lo
    /// sobreescribe (con <c>null</c> si la fuente no lo trae), asi que no se filtra de una invocacion
    /// anterior.
    /// </summary>
    public static bool TryObtenerMembershipId(out string? organizationMembershipId)
    {
        organizationMembershipId = string.IsNullOrWhiteSpace(_organizationMembershipId.Value)
            ? null
            : _organizationMembershipId.Value;
        return organizationMembershipId is not null;
    }

    private static string AssertValue(string? value, string contextField, string headerHttp, string propertyAsb)
        => string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"No se pudo resolver el {contextField} de la invocación actual. En HTTP debe venir del header " +
                $"confiable del gateway ({headerHttp}); en Service Bus, de la ApplicationProperty del mensaje " +
                $"({propertyAsb}, que estampan los senders de Cosmos).")
            : value;
}
