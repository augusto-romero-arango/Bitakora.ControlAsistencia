using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarTurnoADia;

public partial class AsignarTurnoADiaTool(ProgramacionApi programacion)
{
    internal const string NombreTool = "asignar_turno_a_dia";

    public Task<string> Run(
        ToolInvocationContext context,
        string plantilla,
        string turno,
        string dia,
        int? semana,
        CancellationToken ct)
        => throw new NotImplementedException();
}

public sealed record TurnoAsignadoResumen(string Resultado, string Plantilla, int Semana, string Dia, string Turno, string Nota);
