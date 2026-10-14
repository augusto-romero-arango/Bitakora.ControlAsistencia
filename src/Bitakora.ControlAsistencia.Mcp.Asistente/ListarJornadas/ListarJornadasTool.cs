using System.Net.Http.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ListarJornadas;

public partial class ListarJornadasTool(ProgramacionApi api)
{
    internal const string NombreTool = "listar_jornadas";
    internal const int MaximoJornadas = 50;

    [Function("ListarJornadas")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Lista el catalogo de Jornadas: los limites de tiempo de trabajo contra los que se verifica "
            + "la programacion (horas semanales, tope diario, minimo diario y dias de descanso por semana). "
            + "Cada Jornada se identifica por su jornada_id y se describe por sus limites; marca cual es la "
            + "predeterminada, la que se estampa a quien no tiene una asignada.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "cursor",
            "Cursor opaco de la pagina siguiente, tal como lo devolvio la llamada anterior.")]
        string? cursor,
        CancellationToken ct)
    {
        var respuesta = await api.ListarJornadas(MaximoJornadas, cursor, ct);
        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return string.Format(Mensajes.RechazoDelDominio, fallo);

        var sobre = (await respuesta.Content.ReadFromJsonAsync<SobreDeLista<JornadaDelCatalogo>>(ct))!;
        var visibles = sobre.Elementos.Select(j => j.Describir()).ToList();

        return RespuestaJson.Serializar(new CatalogoDeJornadas(
            visibles.Count,
            visibles,
            sobre.SiguienteCursor,
            sobre.SiguienteCursor is null ? null : Mensajes.NotaMasJornadas));
    }
}

/// <summary>Respuesta de listar_jornadas: modalidad catalogo de CA-ADR-0039, sin total.</summary>
public sealed record CatalogoDeJornadas(
    int Mostrando,
    IReadOnlyList<JornadaDescrita> Jornadas,
    string? SiguienteCursor,
    string? Nota);
