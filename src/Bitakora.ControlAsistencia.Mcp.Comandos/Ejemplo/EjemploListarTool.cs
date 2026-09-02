using System.Net.Http.Json;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Ejemplo;

// Tool de EJEMPLO generada por /scaffold-mcp (MEF-ADR-0047 decision 4): reemplazala por las tools
// reales de tu BC. El nombre, la descripcion y el remodelado deben salir del lenguaje ubicuo real
// del dominio (MEF-ADR-0040) -- el texto de abajo es deliberadamente generico. La fuente de datos
// (GET api/programacion/turnos, el catalogo de turnos de Programacion) es real y no un placeholder:
// asi la tool y sus smoke tests (MEF-ADR-0048 seccion 2) ejercitan un endpoint vivo desde el
// primer deploy.
public partial class EjemploListarTool(ProgramacionApi api)
{
    internal const string NombreTool = "ejemplo_listar";
    internal const int MaximoElementos = 50;
    internal const int MaximoLargoFiltro = 100;

    [Function("EjemploListar")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "EJEMPLO: lista el catalogo de turnos de Programacion expuesto por este servidor. La "
            + "lista se trunca cuando es larga; usa filtro_nombre para acotarla. Reemplaza esta "
            + "descripcion por el lenguaje ubicuo real de tu BC antes de publicar la tool.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "filtro_nombre",
            "Texto a buscar dentro del nombre (sin distinguir mayusculas ni acentos).")]
        string? filtroNombre,
        CancellationToken ct)
    {
        // Validacion con mensaje .resx (MEF-ADR-0047 "mensajes runtime en .resx", MEF-ADR-0048
        // seccion 2 -- nivel 3 exige un error path verificable sin tocar ningun dominio): corta
        // antes de llamar al API cuando el filtro es un abuso obvio del parametro.
        if (!string.IsNullOrWhiteSpace(filtroNombre) && filtroNombre.Length > MaximoLargoFiltro)
            return string.Format(Mensajes.ErrorFiltroDemasiadoLargo, MaximoLargoFiltro);

        var respuesta = await api.ListarElementos(ct);
        respuesta.EnsureSuccessStatusCode();

        var elementos = await respuesta.Content.ReadFromJsonAsync<IReadOnlyList<ElementoDto>>(ct) ?? [];

        if (!string.IsNullOrWhiteSpace(filtroNombre))
            elementos = [.. elementos.Where(e => FiltroDeNombre.Contiene(e.Nombre, filtroNombre))];

        var visibles = elementos.Take(MaximoElementos)
            .Select(e => new ElementoResumido(e.Id, e.Nombre.Trim()))
            .ToList();

        var nota = elementos.Count > visibles.Count
            ? string.Format(Mensajes.NotaTruncado, visibles.Count, elementos.Count)
            : null;

        return RespuestaJson.Serializar(new CatalogoDeEjemplos(elementos.Count, visibles.Count, nota, visibles));
    }
}

/// <summary>Forma cruda del elemento tal como lo devuelve la Function App de Programacion.</summary>
internal sealed record ElementoDto(string Id, string Nombre, string? Detalle);

/// <summary>Contrato de respuesta de ejemplo_listar hacia el asistente (remodelado token-eficiente).</summary>
public sealed record CatalogoDeEjemplos(int Total, int Mostrando, string? Nota, IReadOnlyList<ElementoResumido> Elementos);

public sealed record ElementoResumido(string Id, string Nombre);
