using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ListarPlantillasSemanales;

public partial class ListarPlantillasSemanalesTool(ProgramacionApi api)
{
    internal const string NombreTool = "listar_plantillas_semanales";
    internal const int MaximoPlantillas = 50;

    public Task<string> Run(ToolInvocationContext context, string? filtroNombre, CancellationToken ct)
        => throw new NotImplementedException();
}

public sealed record CatalogoDePlantillasSemanales(
    int Total,
    int Mostrando,
    string? Nota,
    IReadOnlyList<PlantillaResumida> Plantillas);

public sealed record PlantillaResumida(
    string Id,
    string Nombre,
    int Semanas,
    bool? Incompleta);
