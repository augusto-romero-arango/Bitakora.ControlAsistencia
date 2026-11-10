using Bitakora.ControlAsistencia.TenantResolver;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

// Lee el Authorization del ToolInvocationContext y no de FunctionContext.GetHttpContext(): una
// invocacion por McpToolTrigger no llega al worker con HttpContext (ver AutorizacionMcpMiddleware,
// "LIMITE ESTRUCTURAL").
public sealed partial class IdentidadTenantMcpMiddleware(
    IValidadorTokenAuthKit validador, IDerivadorIdentidadTenantMcp derivador) : IFunctionsWorkerMiddleware
{
    internal const string EncabezadoAutorizacion = "Authorization";
    internal const string EsquemaBearer = "Bearer ";

// Literal de Microsoft.Azure.Functions.Worker.Extensions.Mcp.Constants.McpToolTriggerBindingType,
// que es internal a ese ensamblado.
    private const string McpToolTriggerBindingType = "mcpToolTrigger";

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var (identidad, sesion) = await DerivarAsync(
            await LeerEncabezadoAutorizacionAsync(context), context.CancellationToken);

        if (sesion is not null)
            context.Items[SesionUsuario.ClaveItems] = sesion;

// El AsyncLocal se muta aqui y no en un metodo async propio: el CLR restaura el contexto de ejecucion
// al retornar de un metodo async y el llamador no veria la mutacion, aunque el await resuelva sincronico.
        if (identidad is not null)
            TenantExecutionContext.SetDerivedIdentity(identidad.TenantId, identidad.UserId, identidad.OrganizationMembershipId);

        await next(context);
    }

// Nucleo testable: opera sobre el encabezado ya extraido porque FunctionContext/ToolInvocationContext
// son inalcanzables en un unit test (MEF-ADR-0048 seccion 1). Sin Bearer no hay identidad: el
// propagador cae al tenant fijo de ConfiguracionIdentidadTenant.
    internal async Task<IdentidadTenant?> DerivarIdentidadAsync(
        string? encabezadoAutorizacion, CancellationToken cancellationToken = default) =>
        (await DerivarAsync(encabezadoAutorizacion, cancellationToken)).Identidad;

    internal async Task<(IdentidadTenant? Identidad, SesionUsuario? Sesion)> DerivarAsync(
        string? encabezadoAutorizacion, CancellationToken cancellationToken = default)
    {
        if (encabezadoAutorizacion is null ||
            !encabezadoAutorizacion.StartsWith(EsquemaBearer, StringComparison.OrdinalIgnoreCase))
            return (null, null);

        var token = encabezadoAutorizacion[EsquemaBearer.Length..];
        // Bearer presente pero no validable no es "sin Bearer": hay un usuario y no sabemos cual. Caer al
        // tenant fijo escribiria sus hechos de negocio en la empresa equivocada, en silencio.
        var principal = await validador.ValidarAsync(token, cancellationToken)
                        ?? throw new InvalidOperationException(Mensajes.TokenNoValidado);

        var identidad = derivador.Derivar(principal);
        return (identidad, SesionUsuario.Desde(principal));
    }

    private static async Task<string?> LeerEncabezadoAutorizacionAsync(FunctionContext context)
    {
        if (context.FunctionDefinition.InputBindings.Values
                .FirstOrDefault(b => b.Type == McpToolTriggerBindingType) is not { } bindingMcp)
            return null;

        var invocacion = (await context.BindInputAsync<ToolInvocationContext>(bindingMcp)).Value;
        return invocacion is not null &&
               invocacion.TryGetHttpTransport(out var transporte) &&
               transporte is not null &&
               transporte.Headers.TryGetValue(EncabezadoAutorizacion, out var valor)
            ? valor
            : null;
    }
}
