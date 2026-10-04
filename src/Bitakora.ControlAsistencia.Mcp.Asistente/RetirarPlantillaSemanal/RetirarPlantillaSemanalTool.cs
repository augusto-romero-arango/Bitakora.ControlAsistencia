using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.RetirarPlantillaSemanal;

public partial class RetirarPlantillaSemanalTool(ProgramacionApi programacion)
{
    internal const string NombreTool = "retirar_plantilla_semanal";

    public Task<string> Run(ToolInvocationContext context, string plantilla, CancellationToken ct)
        => throw new NotImplementedException();
}

public sealed record PlantillaRetiradaResumen(string Resultado, PlantillaRetiradaEco Plantilla, string Nota);

public sealed record PlantillaRetiradaEco(string Id, string Nombre);
