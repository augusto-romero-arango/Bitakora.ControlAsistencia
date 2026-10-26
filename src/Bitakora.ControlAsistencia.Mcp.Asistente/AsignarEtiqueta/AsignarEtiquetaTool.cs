using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarEtiqueta;

public partial class AsignarEtiquetaTool(
    ColaboradoresApi colaboradores, ResolutorCandidatosPorLista resolutorCandidatos)
{
    internal const string NombreTool = "asignar_etiqueta";

    [Function("AsignarEtiqueta")]
    public Task<string> Run(
        [McpToolTrigger(NombreTool, "Pendiente.")]
        ToolInvocationContext context,
        [McpToolProperty("categoria", "Pendiente.", isRequired: false)]
        string categoria,
        [McpToolProperty("valor", "Pendiente.", isRequired: false)]
        string valor,
        [McpToolProperty("identificaciones", "Pendiente.", isRequired: false)]
        string identificaciones,
        CancellationToken ct)
        => throw new NotImplementedException();
}
