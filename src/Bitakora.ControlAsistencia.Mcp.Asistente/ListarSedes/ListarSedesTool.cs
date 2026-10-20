using System.Net.Http.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ListarSedes;

// Catalogo (CA-ADR-0039): pagina con el cursor del backend y nunca descarta un elemento recibido.
// Se podan el Id tecnico (stream key "s:{codigo}", CA-ADR-0031 -- el codigo puro es el
// vocabulario del consumidor), los dispositivos, el centro de costos y la bandera Activa (siempre
// true tras el filtro upstream: seria ruido de tokens).
public partial class ListarSedesTool(SedesApi api)
{
    internal const string NombreTool = "listar_sedes";
    internal const int MaximoSedes = 50;

    [Function("ListarSedes")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Lista las sedes activas de la empresa: codigo, nombre, ciudad y direccion. "
            + "Muestra 50 por pagina; si trae siguienteCursor, pasalo como cursor. Para ubicar una "
            + "sede que el usuario nombra de forma aproximada usa buscar_sedes.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("cursor", "Cursor opaco de la pagina siguiente (el siguienteCursor de la respuesta anterior); reenvialo tal cual.")]
        string? cursor,
        CancellationToken ct)
    {
        var respuesta = await api.ListarFichasActivas(
            ct, MaximoSedes, string.IsNullOrWhiteSpace(cursor) ? null : cursor.Trim());
        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return string.Format(Mensajes.RechazoDelDominio, fallo);

        var sobre = await respuesta.Content.ReadFromJsonAsync<SobreDeLista<FichaSede>>(ct)
            ?? new SobreDeLista<FichaSede>([]);

        if (sobre.Elementos.Count == 0)
            return Mensajes.SinSedes;

        var sedes = sobre.Elementos
            .Select(f => new SedeResumida(f.Codigo, f.Nombre, f.Ciudad, f.Direccion))
            .ToList();

        return RespuestaJson.Serializar(new CatalogoDeSedes(
            sedes.Count,
            sedes,
            sobre.SiguienteCursor,
            sobre.SiguienteCursor is null ? null : string.Format(Mensajes.NotaHayMasSedes, sobre.SiguienteCursor)));
    }
}

public sealed record CatalogoDeSedes(
    int Mostrando,
    IReadOnlyList<SedeResumida> Sedes,
    string? SiguienteCursor,
    string? Nota);

public sealed record SedeResumida(
    string Codigo,
    string Nombre,
    string? Ciudad,
    string? Direccion);
