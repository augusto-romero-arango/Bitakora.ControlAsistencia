using System.Net.Http.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ListarPlantillasSemanales;

// Catalogo (CA-ADR-0039): pagina con el cursor del backend y nunca descarta un elemento recibido.
// Solo remodela: la respuesta del backend ya viene compuesta, no se junta con otras lecturas
// (CA-ADR-0034 decision 5).
public partial class ListarPlantillasSemanalesTool(ProgramacionApi api)
{
    internal const string NombreTool = "listar_plantillas_semanales";
    internal const int MaximoPlantillas = 50;

    [Function("ListarPlantillasSemanales")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Lista las plantillas semanales de turnos del catalogo: nombre, numero de semanas y si "
            + "esta lista para usarse. Las incompletas y las que no cumplen su Jornada aparecen "
            + "marcadas. Muestra 50 por pagina; si trae siguienteCursor, pasalo como cursor. Para "
            + "ubicar una plantilla que el usuario nombra de forma aproximada usa "
            + "buscar_plantillas_semanales; para su cuadro, obtener_plantilla_semanal.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("cursor", "Cursor opaco de la pagina siguiente (el siguienteCursor de la respuesta anterior); reenvialo tal cual.")]
        string? cursor,
        CancellationToken ct)
    {
        var respuesta = await api.ListarPlantillasSemanales(
            ct, MaximoPlantillas, string.IsNullOrWhiteSpace(cursor) ? null : cursor.Trim());
        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return string.Format(Mensajes.RechazoDelDominio, fallo);

        var sobre = await respuesta.Content.ReadFromJsonAsync<SobreDeLista<CuadroSemanalTurnos>>(ct)
            ?? new SobreDeLista<CuadroSemanalTurnos>([]);

        if (sobre.Elementos.Count == 0)
            return Mensajes.SinPlantillas;

        var visibles = sobre.Elementos
            .Select(c => new PlantillaResumida(
                c.Id,
                c.Nombre.Trim(),
                c.Semanas,
                Incompleta: c.Completa ? null : true,
                ConAdvertencias: c.Advertencias is { Count: > 0 } ? true : null))
            .ToList();

        return RespuestaJson.Serializar(new CatalogoDePlantillasSemanales(
            visibles.Count,
            visibles,
            sobre.SiguienteCursor,
            sobre.SiguienteCursor is null
                ? null
                : string.Format(Mensajes.NotaHayMasPlantillas, sobre.SiguienteCursor)));
    }
}

public sealed record CatalogoDePlantillasSemanales(
    int Mostrando,
    IReadOnlyList<PlantillaResumida> Plantillas,
    string? SiguienteCursor,
    string? Nota);

/// <summary>
/// Incompleta y ConAdvertencias viajan solo cuando son true: null se omite en la serializacion
/// (filtro de relevancia, MEF-ADR-0047 decision 4).
/// </summary>
public sealed record PlantillaResumida(
    string Id,
    string Nombre,
    int Semanas,
    bool? Incompleta,
    bool? ConAdvertencias);
