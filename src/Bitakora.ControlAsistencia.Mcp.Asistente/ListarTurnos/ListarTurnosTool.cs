using System.Net.Http.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ListarTurnos;

// Tool de solo lectura sobre GET programacion/turnos. La respuesta se remodela para
// token-eficiencia (issue #502): id + nombre + horario por turno, sin franjas ni descripciones
// (ese detalle es de obtener_turno), y la lista se trunca con senal. filtro_nombre es una
// desviacion documentada del alcance original ("sin parametros"): con un catalogo de miles el
// truncado sin filtro dejaria turnos inalcanzables para el asistente.
//
// El catalogo solo contiene turnos activos: FichaTurnoProjection borra la ficha al TurnoRetirado
// (ShouldDelete), asi que no hay estado que filtrar aqui (revision del PR #512).
public partial class ListarTurnosTool(ProgramacionApi api, ResolutorSedePorCodigo resolutorSedes)
{
    internal const string NombreTool = "listar_turnos";
    internal const int MaximoTurnos = 50;

    [Function("ListarTurnos")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Lista el catalogo de turnos disponibles para programar: id, nombre y horario de cada uno. "
            + "La lista se trunca cuando es larga; usa filtro_nombre para encontrar un turno especifico. "
            + "Para ver la composicion completa de un turno usa obtener_turno con su id. "
            + "Los turnos marcados con enConstruccion no tienen franjas todavia y no se pueden "
            + "programar: sirven para retomar un diseno con agregar_franja.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("sede", "Codigo de la sede para ver solo los turnos con alguna franja en ella.")]
        string? sede,
        [McpToolProperty("cursor", "Cursor opaco de la pagina siguiente (el siguienteCursor de la respuesta anterior); reenvialo tal cual.")]
        string? cursor,
        CancellationToken ct)
    {
        _ = (api, resolutorSedes);
        await Task.CompletedTask;
        throw new NotImplementedException();
    }
}

/// <summary>Contrato de respuesta de listar_turnos hacia el asistente (remodelado, issue #502).</summary>
public sealed record CatalogoDeTurnos(
    int Mostrando,
    IReadOnlyList<TurnoResumido> Turnos,
    string? SiguienteCursor,
    string? Nota);

/// <summary>
/// EsDescanso y EnConstruccion viajan solo cuando son true: null se omite en la serializacion
/// (filtro de relevancia, MEF-ADR-0047 decision 4).
/// </summary>
public sealed record TurnoResumido(
    string Id,
    string Nombre,
    string Horario,
    bool? EsDescanso,
    bool? EnConstruccion);
