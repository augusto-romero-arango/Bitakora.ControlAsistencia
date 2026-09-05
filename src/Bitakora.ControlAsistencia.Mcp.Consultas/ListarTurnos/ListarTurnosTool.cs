using System.Net.Http.Json;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.ListarTurnos;

// Tool de solo lectura sobre GET programacion/turnos. La respuesta se remodela para
// token-eficiencia (issue #502): id + nombre + horario por turno, sin franjas ni descripciones
// (ese detalle es de obtener_turno), y la lista se trunca con senal. filtro_nombre es una
// desviacion documentada del alcance original ("sin parametros"): con un catalogo de miles el
// truncado sin filtro dejaria turnos inalcanzables para el asistente.
//
// El catalogo solo contiene turnos activos: FichaTurnoProjection borra la ficha al TurnoRetirado
// (ShouldDelete), asi que no hay estado que filtrar aqui (revision del PR #512).
public partial class ListarTurnosTool(ProgramacionApi api)
{
    internal const string NombreTool = "listar_turnos";
    internal const int MaximoTurnos = 50;

    [Function("ListarTurnos")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Lista el catalogo de turnos disponibles para programar: id, nombre y horario de cada uno. "
            + "La lista se trunca cuando es larga; usa filtro_nombre para encontrar un turno especifico. "
            + "Para ver la composicion completa de un turno usa obtener_turno con su id.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "filtro_nombre",
            "Texto a buscar dentro del nombre del turno (sin distinguir mayusculas ni acentos).")]
        string? filtroNombre,
        CancellationToken ct)
    {
        var respuesta = await api.ListarTurnos(ct);
        respuesta.EnsureSuccessStatusCode();

        var fichas = await respuesta.Content.ReadFromJsonAsync<IReadOnlyList<FichaTurno>>(ct)
            ?? [];

        if (!string.IsNullOrWhiteSpace(filtroNombre))
            fichas = [.. fichas.Where(f => FiltroDeNombre.Contiene(f.Nombre, filtroNombre))];

        var visibles = fichas.Take(MaximoTurnos)
            .Select(f => new TurnoResumido(
                f.Id,
                f.Nombre.Trim(),
                f.HorarioResumido,
                f.EsDescanso ? true : null))
            .ToList();

        var nota = fichas.Count > visibles.Count
            ? string.Format(Mensajes.NotaTruncado, visibles.Count, fichas.Count)
            : null;

        return RespuestaJson.Serializar(new CatalogoDeTurnos(fichas.Count, visibles.Count, nota, visibles));
    }
}

/// <summary>Contrato de respuesta de listar_turnos hacia el asistente (remodelado, issue #502).</summary>
public sealed record CatalogoDeTurnos(
    int Total,
    int Mostrando,
    string? Nota,
    IReadOnlyList<TurnoResumido> Turnos);

/// <summary>EsDescanso viaja solo cuando es true: null se omite en la serializacion.</summary>
public sealed record TurnoResumido(
    string Id,
    string Nombre,
    string Horario,
    bool? EsDescanso,
    bool? EnConstruccion = null);
