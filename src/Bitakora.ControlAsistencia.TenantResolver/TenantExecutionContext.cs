using Cosmos.MultiTenancy;

namespace Bitakora.ControlAsistencia.TenantResolver;

/// <summary>
/// Identidad (tenant + usuario) de la invocacion en curso. La puebla <see cref="TenantContextMiddleware"/>
/// al inicio de cada invocacion desde el contexto de ejecucion del trigger (headers HTTP o
/// ApplicationProperties del mensaje de Service Bus); los routers/senders de Wolverine la consumen via
/// <see cref="ITenantResolver"/>.
///
/// El estado vive en <see cref="AsyncLocal{T}"/> (ambiente por invocacion), no en campos de instancia,
/// a proposito (MEF-ADR-0032, patron de la implementacion de referencia Cosmos.ControlPlane): Wolverine
/// genera el codigo de sus handlers creando su PROPIO <c>IServiceScope</c> hijo del contenedor raiz
/// (<c>IServiceScopeFactory.CreateAsyncScope()</c>; ver JasperFx.CodeGeneration
/// <c>LazyServiceLocationFrame</c>), distinto del scope de la invocacion de Functions que puebla el
/// middleware. Un holder con estado por instancia NO le llegaria al handler: seria otra instancia,
/// vacia. El AsyncLocal fluye por la cadena async (middleware -&gt; <c>InvokeInlineAsync</c> -&gt;
/// handler) sin depender del scope de DI, asi que la identidad cruza ese limite. Mismo patron que
/// <c>IHttpContextAccessor</c>.
///
/// Este mismo limite de scope es el que rompio a <c>ProxyTenantResolver</c> de
/// Cosmos.MultiTenancy.CritterStack en el worker aislado: decide la rama (headers vs IMessageContext)
/// en su CONSTRUCTOR segun <c>IHttpContextAccessor.HttpContext</c>, que es null en el momento en que
/// el grafo de DI lo construye, asi que toda request HTTP caia en la rama de Wolverine y fallaba.
///
/// Como el estado es ambiente, la instancia es un lector sin estado: se registra <b>singleton</b> (una
/// basta) y da igual que Wolverine inyecte esa u otra instancia. Los unicos escritores son
/// <see cref="Set"/> (el middleware, caso normal) y <see cref="SetDerivedIdentity"/> (triggers sin
/// identidad del gateway).
///
/// Los getters de <see cref="ITenantResolver"/> fallan ruidosamente si la identidad no se resolvio,
/// para que un fallo del gateway o un mensaje sin identidad no pase desapercibido.
/// </summary>
public sealed class TenantExecutionContext : ITenantResolver
{
    private static readonly AsyncLocal<string?> _tenantId = new();
    private static readonly AsyncLocal<string?> _userId = new();

    /// <summary>
    /// Puebla la identidad de la invocacion en curso desde el contexto del trigger. Unico escritor:
    /// <see cref="TenantContextMiddleware"/>.
    /// </summary>
    internal static void Set(string? tenantId, string? userId)
    {
        _tenantId.Value = tenantId;
        _userId.Value = userId;
    }

    /// <summary>
    /// Puebla la identidad cuando el trigger no la recibe del gateway porque el llamador no puede
    /// presentar un JWT (webhooks de un proveedor externo, timers). El <paramref name="tenantId"/> es
    /// el tenant de EJECUCION -- el mismo que el gateway estampa desde el claim <c>tenant_id</c> en el
    /// resto de las Functions, y que decide la particion de Marten. El <paramref name="actor"/> nombra
    /// al proceso que escribe, porque no hay usuario detras.
    ///
    /// Hace falta porque el sender del harness lee <c>ITenantResolver.TenantId</c> y <c>.UserId</c> en
    /// cada publish (<c>Cosmos.EventDriven.CritterStack.TenancyDelivery</c>): sin identidad ambiente
    /// no se puede publicar. De ahi que ambos parametros sean obligatorios -- un actor nulo haria
    /// fallar el publish, no el caller.
    ///
    /// No usar desde Functions HTTP detras del gateway: ahi la identidad ya la puebla el middleware a
    /// partir de los headers, y sobreescribirla solo la enmascara.
    /// </summary>
    public static void SetDerivedIdentity(string tenantId, string actor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);

        Set(tenantId, actor);
    }

    public string TenantId => AssertValue(_tenantId.Value, "tenant");

    public string UserId => AssertValue(_userId.Value, "usuario");

    /// <summary>
    /// Version sin lanzar de los getters, para un consumidor que debe decidir entre la identidad
    /// ambiente y un fallback propio sin usar <see cref="InvalidOperationException"/> como control
    /// de flujo (segundo consumidor: <c>PropagadorIdentidadTenantHandler</c> del servidor MCP,
    /// issue #540 -- prefiere la identidad ambiente derivada del token y cae al tenant fijo de
    /// <c>ConfiguracionIdentidadTenant</c> solo cuando esta no esta poblada). Retorna
    /// <c>false</c> si cualquiera de los dos valores no esta poblado; en ese caso ambos out quedan
    /// en <c>null</c>, nunca uno poblado y el otro no.
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

    private static string AssertValue(string? value, string contextField)
        => string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"No se pudo resolver el {contextField} de la invocación actual. En HTTP debe venir del header " +
                "confiable del gateway (X-Tenant-Id/X-User-Id); en Service Bus, de las ApplicationProperties " +
                "del mensaje (tenant-id/user_id, que estampan los senders de Cosmos).")
            : value;
}
