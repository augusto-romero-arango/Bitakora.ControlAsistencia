using System.Net.Http.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ListarTurnos;

// Catalogo (CA-ADR-0039): pagina con el cursor del backend y nunca descarta un elemento recibido.
// sede se traduce a SedeId sin exigir que la sede este activa.
// El catalogo solo contiene turnos activos: FichaTurnoProjection borra la ficha al TurnoRetirado,
// asi que aqui no hay estado que filtrar.
public partial class ListarTurnosTool(ProgramacionApi api, ResolutorSedePorCodigo resolutorSedes)
{
    internal const string NombreTool = "listar_turnos";
    internal const int MaximoTurnos = 50;

    [Function("ListarTurnos")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Lista el catalogo de turnos disponibles para programar: id, nombre y horario de cada uno. "
            + "Filtra por sede (codigo): los turnos con alguna franja en esa sede. Muestra 50 por "
            + "pagina; si trae siguienteCursor, pasalo como cursor para la pagina siguiente. Para "
            + "ubicar un turno que el usuario nombra de forma aproximada usa buscar_turnos; para su "
            + "composicion, obtener_turno. Los turnos marcados con enConstruccion no tienen franjas "
            + "todavia y no se pueden programar: sirven para retomar un diseno con agregar_franja.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("sede", "Codigo de la sede para ver solo los turnos con alguna franja en ella.")]
        string? sede,
        [McpToolProperty("cursor", "Cursor opaco de la pagina siguiente (el siguienteCursor de la respuesta anterior); reenvialo tal cual.")]
        string? cursor,
        CancellationToken ct)
    {
        var sedeCodigo = string.IsNullOrWhiteSpace(sede) ? null : sede.Trim();
        string? sedeId = null;
        if (sedeCodigo is not null)
        {
            var resolucion = await resolutorSedes.ResolverIdAsync(sedeCodigo, ct);
            if (resolucion.FalloDeLectura is { } falloSede)
                return string.Format(Mensajes.RechazoDelDominio, falloSede);
            if (resolucion.NoExiste)
                return string.Format(Mensajes.SedeNoExiste, sedeCodigo);
            sedeId = resolucion.SedeId;
        }

        var respuesta = await api.ListarTurnos(
            ct, MaximoTurnos, string.IsNullOrWhiteSpace(cursor) ? null : cursor.Trim(), sedeId);
        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return string.Format(Mensajes.RechazoDelDominio, fallo);

        var sobre = await respuesta.Content.ReadFromJsonAsync<SobreDeLista<FichaTurno>>(ct)
            ?? new SobreDeLista<FichaTurno>([]);

        if (sobre.Elementos.Count == 0)
            return sedeCodigo is null
                ? Mensajes.SinTurnos
                : string.Format(Mensajes.SinTurnosEnSede, sedeCodigo);

        var visibles = sobre.Elementos
            .Select(f => new TurnoResumido(
                f.Id,
                f.Nombre.Trim(),
                f.HorarioAbierto is { } ha ? string.Format(Mensajes.HorarioAbierto, ha.Duracion) : f.HorarioResumido,
                EsDescanso: f.EsDescanso ? true : null,
                EnConstruccion: f.Completo ? null : true,
                ConVentanaDeEntrada: f.VentanaDeEntrada is not null && f.HorarioAbierto is null ? true : null))
            .ToList();

        return RespuestaJson.Serializar(new CatalogoDeTurnos(
            visibles.Count,
            visibles,
            sobre.SiguienteCursor,
            sobre.SiguienteCursor is null ? null : string.Format(Mensajes.NotaHayMasTurnos, sobre.SiguienteCursor)));
    }
}

public sealed record CatalogoDeTurnos(
    int Mostrando,
    IReadOnlyList<TurnoResumido> Turnos,
    string? SiguienteCursor,
    string? Nota);

/// <summary>
/// EsDescanso, EnConstruccion y ConVentanaDeEntrada viajan solo cuando son true: null se omite en la serializacion
/// (filtro de relevancia, MEF-ADR-0047 decision 4).
/// </summary>
public sealed record TurnoResumido(
    string Id,
    string Nombre,
    string Horario,
    bool? EsDescanso,
    bool? EnConstruccion,
    bool? ConVentanaDeEntrada);
