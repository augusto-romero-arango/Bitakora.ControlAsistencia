using System.Text.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.CrearPlantillaSemanal;

public partial class CrearPlantillaSemanalTool(ProgramacionApi programacion)
{
    internal const string NombreTool = "crear_plantilla_semanal";
    internal const int MinimoSemanas = 1;
    internal const int MaximoSemanas = 6;
    internal const int DiasPorSemana = 7;

    public Task<string> Run(
        ToolInvocationContext context,
        string nombre,
        int? semanas,
        string dias,
        CancellationToken ct)
        => throw new NotImplementedException();
}

public sealed record PlantillaCreadaResumen(
    string Resultado,
    PlantillaResumen Plantilla,
    int DiasAsignados,
    IReadOnlyList<DiaRechazado>? DiasRechazados,
    bool Completa,
    string Nota);

public sealed record PlantillaResumen(string Id, string Nombre, int Semanas);

public sealed record DiaRechazado(int Semana, string Dia, string Turno, string Motivo);

public sealed record DiaDePlantillaEntrada(int? Semana, JsonElement Dia, string? Turno);
