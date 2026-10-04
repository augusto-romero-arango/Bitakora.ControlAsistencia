using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ObtenerPlantillaSemanal;

public partial class ObtenerPlantillaSemanalTool(ProgramacionApi api)
{
    internal const string NombreTool = "obtener_plantilla_semanal";

    public Task<string> Run(ToolInvocationContext context, string plantilla, CancellationToken ct)
        => throw new NotImplementedException();
}

public sealed record PlantillaSemanalDetallada(
    string Id,
    string Nombre,
    int Semanas,
    bool Completa,
    IReadOnlyList<SemanaDelCuadro> Cuadro);

public sealed record SemanaDelCuadro(
    int Semana,
    string Lunes,
    string Martes,
    string Miercoles,
    string Jueves,
    string Viernes,
    string Sabado,
    string Domingo);
