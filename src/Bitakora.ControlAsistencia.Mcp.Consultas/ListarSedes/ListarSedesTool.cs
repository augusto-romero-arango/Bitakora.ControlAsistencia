using System.Net.Http.Json;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.ListarSedes;

// Tool de solo lectura sobre GET sedes/fichas. Remodelado (issue #502): se podan el Id tecnico
// (stream key "s:{codigo}", CA-ADR-0031 -- el codigo puro es el vocabulario del consumidor), los
// dispositivos y el centro de costos, que no participan en la conversacion de programar turnos.
// filtro_nombre: misma desviacion documentada que en listar_turnos.
public class ListarSedesTool(SedesApi api)
{
    internal const string NombreTool = "listar_sedes";
    internal const int MaximoSedes = 50;

    [Function("ListarSedes")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Lista las sedes de la empresa: codigo, nombre, ciudad, direccion y si esta activa "
            + "para asignacion. La lista se trunca cuando es larga; usa filtro_nombre para "
            + "encontrar una sede especifica.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "filtro_nombre",
            "Texto a buscar dentro del nombre de la sede (sin distinguir mayusculas ni acentos).")]
        string? filtroNombre,
        CancellationToken ct)
    {
        var respuesta = await api.ListarFichas(ct);
        respuesta.EnsureSuccessStatusCode();

        var fichas = await respuesta.Content.ReadFromJsonAsync<IReadOnlyList<FichaSede>>(ct)
            ?? [];

        if (!string.IsNullOrWhiteSpace(filtroNombre))
            fichas = [.. fichas.Where(f => FiltroDeNombre.Contiene(f.Nombre, filtroNombre))];

        var visibles = fichas.Take(MaximoSedes)
            .Select(f => new SedeResumida(f.Codigo, f.Nombre, f.Ciudad, f.Direccion, f.Activa))
            .ToList();

        var nota = fichas.Count > visibles.Count
            ? $"Mostrando {visibles.Count} de {fichas.Count} sedes; usa filtro_nombre para refinar."
            : null;

        return RespuestaJson.Serializar(new CatalogoDeSedes(fichas.Count, visibles.Count, nota, visibles));
    }
}

/// <summary>Contrato de respuesta de listar_sedes hacia el asistente (remodelado, issue #502).</summary>
public sealed record CatalogoDeSedes(
    int Total,
    int Mostrando,
    string? Nota,
    IReadOnlyList<SedeResumida> Sedes);

public sealed record SedeResumida(
    string Codigo,
    string Nombre,
    string? Ciudad,
    string? Direccion,
    bool Activa);
