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
        => throw new NotImplementedException();

    internal string Describir(SesionUsuario? sesion) => throw new NotImplementedException();
}
