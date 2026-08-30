using System.Net.Http.Json;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.ListarSedes;

// Tool de solo lectura sobre GET sedes/fichas?activa=true (decisiones de revision del PR #512:
// solo sedes activas, sin tope de truncado). Remodelado (issue #502): se podan el Id tecnico
// (stream key "s:{codigo}", CA-ADR-0031 -- el codigo puro es el vocabulario del consumidor), los
// dispositivos, el centro de costos y la bandera Activa (siempre true tras el filtro upstream:
// seria ruido de tokens). filtro_nombre se conserva como economia de tokens, no como alcance.
public class ListarSedesTool(SedesApi api)
{
    internal const string NombreTool = "listar_sedes";

    [Function("ListarSedes")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Lista las sedes activas de la empresa: codigo, nombre, ciudad y direccion. "
            + "Usa filtro_nombre para encontrar una sede especifica sin traer el catalogo completo.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "filtro_nombre",
            "Texto a buscar dentro del nombre de la sede (sin distinguir mayusculas ni acentos).")]
        string? filtroNombre,
        CancellationToken ct)
    {
        var respuesta = await api.ListarFichasActivas(ct);
        respuesta.EnsureSuccessStatusCode();

        var fichas = await respuesta.Content.ReadFromJsonAsync<IReadOnlyList<FichaSede>>(ct)
            ?? [];

        if (!string.IsNullOrWhiteSpace(filtroNombre))
            fichas = [.. fichas.Where(f => FiltroDeNombre.Contiene(f.Nombre, filtroNombre))];

        var sedes = fichas
            .Select(f => new SedeResumida(f.Codigo, f.Nombre, f.Ciudad, f.Direccion))
            .ToList();

        return RespuestaJson.Serializar(new CatalogoDeSedes(sedes.Count, sedes));
    }
}

/// <summary>Contrato de respuesta de listar_sedes hacia el asistente (remodelado, issue #502).</summary>
public sealed record CatalogoDeSedes(
    int Total,
    IReadOnlyList<SedeResumida> Sedes);

public sealed record SedeResumida(
    string Codigo,
    string Nombre,
    string? Ciudad,
    string? Direccion);
