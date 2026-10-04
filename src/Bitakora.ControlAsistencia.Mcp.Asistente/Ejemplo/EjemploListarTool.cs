using System.Globalization;
using System.Net.Http.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Ejemplo;

// Tool de EJEMPLO generada por /scaffold-mcp (MEF-ADR-0047 decision 4): reemplazala por las tools
// reales de tu BC. El nombre, la descripcion y el remodelado deben salir del lenguaje ubicuo real
// del dominio (MEF-ADR-0040) -- el texto de abajo es deliberadamente generico.
// fecha_referencia no filtra el catalogo: existe para demostrar el patron de validacion de fecha
// string (McpToolProperty + TryParseExact + mensaje .resx) y darle al smoke e2e un camino valido
// verificable -- sin el, ningun parametro de este scaffold prueba que el middleware que restaura
// el texto original coercionado por la extension MCP sigue activo tras un deploy.
public partial class EjemploListarTool(ProgramacionApi api)
{
    internal const string NombreTool = "ejemplo_listar";
    internal const int MaximoElementos = 50;
    internal const int MaximoLargoFiltro = 100;

    [Function("EjemploListar")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "EJEMPLO: lista el catalogo de turnos de Programacion expuesto por este servidor. La lista se "
            + "trunca cuando es larga; usa filtro_nombre para acotarla. Reemplaza esta descripcion por "
            + "el lenguaje ubicuo real de tu BC antes de publicar la tool.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "filtro_nombre",
            "Texto a buscar dentro del nombre (sin distinguir mayusculas ni acentos).")]
        string? filtroNombre,
        [McpToolProperty(
            "fecha_referencia",
            "Fecha de referencia en formato yyyy-MM-dd (opcional; por defecto no filtra).")]
        string? fechaReferencia,
        CancellationToken ct)
    {
        // Validacion con mensaje .resx (MEF-ADR-0047 "mensajes runtime en .resx", MEF-ADR-0048
        // seccion 2 -- nivel 3 exige un error path verificable sin tocar ningun dominio): corta
        // antes de llamar al API cuando el filtro es un abuso obvio del parametro.
        if (!string.IsNullOrWhiteSpace(filtroNombre) && filtroNombre.Length > MaximoLargoFiltro)
            return string.Format(Mensajes.ErrorFiltroDemasiadoLargo, MaximoLargoFiltro);

        // fecha_referencia sigue siendo string (MEF-ADR-0047 decision 1): la extension MCP coerciona
        // todo string con forma de fecha antes de que la tool lo reciba, asi que declarar DateOnly
        // aqui perderia el texto original que el middleware de restauracion recompone.
        if (fechaReferencia is not null
            && !DateOnly.TryParseExact(
                fechaReferencia, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return string.Format(Mensajes.ErrorFechaReferenciaInvalida, fechaReferencia);

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

        return RespuestaJson.Serializar(
            new CatalogoDeEjemplos(elementos.Count, visibles.Count, nota, fechaReferencia, visibles));
    }
}

/// <summary>Forma cruda del elemento tal como lo devuelve la Function App de Programacion.</summary>
internal sealed record ElementoDto(string Id, string Nombre, string? Detalle);

/// <summary>Contrato de respuesta de ejemplo_listar hacia el asistente (remodelado token-eficiente).</summary>
public sealed record CatalogoDeEjemplos(
    int Total, int Mostrando, string? Nota, string? FechaReferencia, IReadOnlyList<ElementoResumido> Elementos);

public sealed record ElementoResumido(string Id, string Nombre);
