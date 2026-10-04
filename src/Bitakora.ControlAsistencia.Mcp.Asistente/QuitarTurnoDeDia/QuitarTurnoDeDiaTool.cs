using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.QuitarTurnoDeDia;

public partial class QuitarTurnoDeDiaTool(ProgramacionApi programacion)
{
    internal const string NombreTool = "quitar_turno_de_dia";

    public Task<string> Run(
        ToolInvocationContext context,
        string plantilla,
        string dia,
        int? semana,
        CancellationToken ct)
        => throw new NotImplementedException();
}

public sealed record TurnoQuitadoResumen(string Resultado, string Plantilla, int Semana, string Dia, string Nota);
