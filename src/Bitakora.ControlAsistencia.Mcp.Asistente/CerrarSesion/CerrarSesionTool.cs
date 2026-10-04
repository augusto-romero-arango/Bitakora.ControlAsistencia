using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.CerrarSesion;

public partial class CerrarSesionTool
{
    internal const string NombreTool = "cerrar_sesion";
    internal const string UrlBaseLogout = "https://api.workos.com/user_management/sessions/logout";

    public Task<string> Run(ToolInvocationContext context, FunctionContext functionContext)
        => throw new NotImplementedException();

    internal string Describir(SesionUsuario? sesion) => throw new NotImplementedException();
}
