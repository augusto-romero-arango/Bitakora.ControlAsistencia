using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.CerrarSesion;

public partial class CerrarSesionTool
{
    internal const string NombreTool = "cerrar_sesion";
    internal const string UrlBaseLogout = "https://api.workos.com/user_management/sessions/logout";

    [Function("CerrarSesion")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Te da el enlace para cerrar tu sesion de autenticacion (WorkOS). Abrelo en el navegador: "
            + "termina tu sesion y olvida la cuenta con la que entraste, para que al reconectar el "
            + "conector puedas elegir otra empresa. El acceso del conector se corta en unos minutos "
            + "(cuando vence el token actual). Sin parametros.")]
        [McpMetadata("""{"readOnlyHint": true, "destructiveHint": false}""")]
        ToolInvocationContext context,
        FunctionContext functionContext)
        => Task.FromResult(Describir(
            functionContext.Items.TryGetValue(SesionUsuario.ClaveItems, out var valor)
                ? valor as SesionUsuario
                : null));

    internal string Describir(SesionUsuario? sesion) => sesion?.SesionId is { } sid
        ? RespuestaJson.Serializar(new RespuestaCierre(
            Mensajes.AbreElEnlace, $"{UrlBaseLogout}?session_id={Uri.EscapeDataString(sid)}", Mensajes.NotaCierre))
        : RespuestaJson.Serializar(new RespuestaCierre(Mensajes.SinSesionQueCerrar, null, null));

    internal sealed record RespuestaCierre(string Resultado, string? Url, string? Nota);
}
