using System.Net.Http.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ListarPlantillas;

public partial class ListarPlantillasTool(ProgramacionApi api)
{
    internal const string NombreTool = "listar_plantillas";
    internal const int MaximoPlantillas = 50;

    [Function("ListarPlantillas")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Lista las plantillas de turnos del catalogo: nombre, dias del ciclo y si esta lista para "
            + "usarse. Las incompletas y las que no cumplen su Jornada aparecen marcadas. Muestra 50 "
            + "por pagina; si trae siguienteCursor, pasalo como cursor. Para ubicar una plantilla que "
            + "el usuario nombra de forma aproximada usa buscar_plantillas; para su cuadro, "
            + "obtener_plantilla.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("cursor", "Cursor opaco de la pagina siguiente (el siguienteCursor de la respuesta anterior); reenvialo tal cual.")]
        string? cursor,
        CancellationToken ct)
    {
        var respuesta = await api.ListarPlantillasTurnos(
            ct, MaximoPlantillas, string.IsNullOrWhiteSpace(cursor) ? null : cursor.Trim());
        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return string.Format(Mensajes.RechazoDelDominio, fallo);

        var sobre = await respuesta.Content.ReadFromJsonAsync<SobreDeLista<CuadroDeTurnos>>(ct)
            ?? new SobreDeLista<CuadroDeTurnos>([]);

        if (sobre.Elementos.Count == 0)
            return Mensajes.SinPlantillas;

        var visibles = sobre.Elementos
            .Select(c => new PlantillaResumida(
                c.Id,
                c.Nombre.Trim(),
                c.DiasDelCiclo,
                Incompleta: c.Completa ? null : true,
                ConAdvertencias: c.Advertencias is { Count: > 0 } ? true : null))
            .ToList();

        return RespuestaJson.Serializar(new CatalogoDePlantillas(
            visibles.Count,
            visibles,
            sobre.SiguienteCursor,
            sobre.SiguienteCursor is null
                ? null
                : string.Format(Mensajes.NotaHayMasPlantillas, sobre.SiguienteCursor)));
    }
}

public sealed record CatalogoDePlantillas(
    int Mostrando,
    IReadOnlyList<PlantillaResumida> Plantillas,
    string? SiguienteCursor,
    string? Nota);

public sealed record PlantillaResumida(
    string Id,
    string Nombre,
    int DiasDelCiclo,
    bool? Incompleta,
    bool? ConAdvertencias);
