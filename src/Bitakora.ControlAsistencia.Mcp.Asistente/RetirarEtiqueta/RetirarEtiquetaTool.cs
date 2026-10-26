using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.RetirarEtiqueta;

public partial class RetirarEtiquetaTool(
    ColaboradoresApi colaboradores, ResolutorCandidatosPorLista resolutorCandidatos)
{
    internal const string NombreTool = "retirar_etiqueta";

    [Function("RetirarEtiqueta")]
    public Task<string> Run(
        [McpToolTrigger(NombreTool, "Pendiente.")]
        ToolInvocationContext context,
        [McpToolProperty("categoria", "Pendiente.", isRequired: false)]
        string categoria,
        [McpToolProperty("identificaciones", "Pendiente.", isRequired: false)]
        string identificaciones,
        CancellationToken ct)
        => throw new NotImplementedException();
}
