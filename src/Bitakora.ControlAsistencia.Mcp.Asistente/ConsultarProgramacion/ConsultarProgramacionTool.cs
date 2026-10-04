using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarProgramacion;

// UNA sola tool de rango sobre QUERY control-horas/turnos-vigentes (decision de refinamiento
// 2026-08-30 del issue #502: tools consolidadas): el caso puntual "que le toca a Juan el 3" es
// desde = hasta + codigo_colaborador, sin tool aparte sobre ObtenerTurnoVigente.
//
// Remodelado: se podan el Id (stream key "cd:...") y el HorarioResumido (los bloques compactos son
// la forma canonica y traen la sede); las fechas invalidas y el rango invertido se responden como
// mensaje sin llamar al dominio; el 422 upstream (rango que el propio dominio rechaza) se traduce
// pasando su mensaje.
public partial class ConsultarProgramacionTool(ControlHorasApi api)
{
    internal const string NombreTool = "consultar_programacion";
    internal const int MaximoDias = 50;

    [Function("ConsultarProgramacion")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Consulta que turno rige a cada colaborador en un rango de fechas (la programacion "
            + "vigente). Filtra opcionalmente por colaborador o por sede. Para un dia puntual usa "
            + "desde = hasta.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("desde", "Fecha inicial del rango, formato yyyy-MM-dd.", isRequired: true)]
        string desde,
        [McpToolProperty("hasta", "Fecha final del rango (inclusive), formato yyyy-MM-dd.", isRequired: true)]
        string hasta,
        [McpToolProperty(
            "codigo_colaborador",
            "Codigo del colaborador para ver solo su programacion; omitelo para el panorama de todos.")]
        string? codigoColaborador,
        [McpToolProperty(
            "sede_id",
            "Id de la sede para ver solo los dias con al menos un bloque en esa sede.")]
        string? sedeId,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Contrato de respuesta de consultar_programacion hacia el asistente (remodelado, issue #502).
/// Desde/Hasta son los aplicados por el dominio, que pueden diferir de los pedidos si hubo recorte
/// (la Nota lo senala).
/// </summary>
public sealed record ProgramacionVigente(
    DateOnly Desde,
    DateOnly Hasta,
    string? Nota,
    int Total,
    int Mostrando,
    IReadOnlyList<DiaProgramado> Turnos);

/// <summary>Un dia de la programacion de un colaborador, con sus bloques ya compactados.</summary>
public sealed record DiaProgramado(
    string Colaborador,
    string? Nombre,
    DateOnly Fecha,
    string Turno,
    IReadOnlyList<string> Bloques);
