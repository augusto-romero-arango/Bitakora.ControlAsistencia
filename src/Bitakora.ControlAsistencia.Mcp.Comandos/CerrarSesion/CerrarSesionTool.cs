using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.CerrarSesion;

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
        => throw new NotImplementedException();

    internal string Describir(SesionUsuario? sesion) => throw new NotImplementedException();
}
