using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.RetirarTurno;

// Cliente HTTP puro de GET programacion/turnos (resolver nombre -> id, via
// ResolutorTurnoPorNombre) + DELETE programacion/turnos/{id} (paso 3 MEF-ADR-0043): referencia
// por nombre, mismo resolutor que solicitar_programacion_turno (MEF-ADR-0018). El 404/409 del
// DELETE se devuelve como texto, nunca como excepcion (CA-ADR-0030).
public partial class RetirarTurnoTool(ProgramacionApi programacion)
{
    internal const string NombreTool = "retirar_turno";

    [Function("RetirarTurno")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Retira un turno del catalogo por su nombre exacto (miralo con listar_turnos): deja "
            + "de poder programarse y su nombre queda libre para reutilizarse. Los dias ya "
            + "programados con ese turno no cambian. Usalo tambien para descartar un turno "
            + "incompleto o un diseno a medias.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "turno",
            "Nombre exacto del turno del catalogo (miralo con listar_turnos).",
            isRequired: true)]
        string turno,
        CancellationToken ct)
        => throw new NotImplementedException();
}

/// <summary>
/// Eco compacto de retirar_turno hacia el asistente: el 202 del dominio no trae body, asi que el
/// turno retirado se reconstruye con la ficha ya resuelta por nombre.
/// </summary>
public sealed record TurnoRetiradoResumen(string Resultado, TurnoRetiradoEco Turno, string Nota);

public sealed record TurnoRetiradoEco(string Id, string Nombre);
