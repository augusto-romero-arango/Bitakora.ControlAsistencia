using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ModificarLimitesJornada;

public partial class ModificarLimitesJornadaTool(ProgramacionApi api)
{
    internal const string NombreTool = "modificar_limites_jornada";

    public Task<string> Run(ToolInvocationContext context, string jornadaId, string horasSemanales, string topeDiario, string minimoDiario, int diasDescansoPorSemana, CancellationToken ct) =>
        throw new NotImplementedException();
}
