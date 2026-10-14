using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.DesactivarJornada;

public partial class DesactivarJornadaTool(
    ProgramacionApi programacion, ColaboradoresApi colaboradores, TimeProvider? reloj = null)
{
    internal const string NombreTool = "desactivar_jornada";

    public Task<string> Run(ToolInvocationContext context, string jornadaId, CancellationToken ct)
        => throw new NotImplementedException();
}
