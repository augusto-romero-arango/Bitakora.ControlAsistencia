using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.CrearJornada;

public partial class CrearJornadaTool(ProgramacionApi api)
{
    internal const string NombreTool = "crear_jornada";

    public Task<string> Run(ToolInvocationContext context, string horasSemanales, string topeDiario, string minimoDiario, int diasDescansoPorSemana, CancellationToken ct) =>
        throw new NotImplementedException();
}
