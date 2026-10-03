using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.ObtenerSesion;

public partial class ObtenerSesionTool(IdentidadTenant tenantFijo)
{
    internal const string NombreTool = "obtener_sesion";

    [Function("ObtenerSesion")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Muestra con que sesion de autenticacion estas operando: tu correo, la empresa "
            + "(organizacion) en la que quedaste al conectar y si la identidad viene de tu sesion "
            + "o del tenant fijo de configuracion. Usala para confirmar en que empresa estas "
            + "despues de reconectar el conector. Sin parametros.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        FunctionContext functionContext)
        => Task.FromResult(Describir(
            functionContext.Items.TryGetValue(SesionUsuario.ClaveItems, out var valor)
                ? valor as SesionUsuario
                : null));

    internal string Describir(SesionUsuario? sesion) => RespuestaJson.Serializar(sesion is null
        ? new RespuestaSesion(
            "tenant_fijo", null, new Empresa(tenantFijo.TenantId, null), null, Mensajes.SinSesionDeUsuario)
        : new RespuestaSesion(
            "sesion", sesion.Correo, new Empresa(sesion.OrganizacionId, sesion.OrganizacionNombre),
            sesion.MembershipId, null));

    internal sealed record Empresa(string Id, string? Nombre);

    internal sealed record RespuestaSesion(
        string Origen, string? Correo, Empresa Empresa, string? Membership, string? Advertencia);
}
