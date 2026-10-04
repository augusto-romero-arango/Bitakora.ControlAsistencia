using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ObtenerSesion;

public partial class ObtenerSesionTool(IdentidadTenant tenantFijo)
{
    internal const string NombreTool = "obtener_sesion";

    public Task<string> Run(ToolInvocationContext context, FunctionContext functionContext)
        => throw new NotImplementedException();

    internal string Describir(SesionUsuario? sesion) => throw new NotImplementedException();
}
