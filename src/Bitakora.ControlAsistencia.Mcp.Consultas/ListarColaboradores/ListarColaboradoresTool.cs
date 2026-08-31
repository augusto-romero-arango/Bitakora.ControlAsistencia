using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.ListarColaboradores;

// Tool de solo lectura sobre QUERY colaboradores/fichas (listado) y GET colaboradores/fichas/{id}
// (consulta puntual) -- issue #530, cierre de la cadena colaborador+sede que #502 dejo fuera de
// alcance. Tool consolidada (decision de refinamiento del issue): identificacion? decide la ruta,
// no hay tool separada para el caso puntual.
//
// Sin enriquecimiento del nombre de sede (MEF-ADR-0018): codigoSede viaja tal cual. TimeProvider
// resuelve fecha_referencia a "hoy" en America/Bogota cuando el parametro llega ausente -- el back
// jamas resuelve "hoy" (decision #373), la tool MCP es el cliente que lo hace por el agente.
public partial class ListarColaboradoresTool(ColaboradoresApi api, TimeProvider reloj)
{
    internal const string NombreTool = "listar_colaboradores";
    internal const int MaximoColaboradores = 20;
    internal const int TakeUpstream = 200;

    [Function("ListarColaboradores")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Lista los colaboradores vinculados: identificacion, nombre, sede y etiquetas de cada "
            + "uno. Filtra opcionalmente por sede o por etiquetas (pares categoria:valor, AND entre "
            + "ellos). Si envias identificacion consultas la ficha puntual de ese colaborador y los "
            + "demas filtros no aplican. Sin fecha_referencia se usa el dia de hoy.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "identificacion",
            "Identificacion puntual del colaborador (ej. 'CC-123456'). Si se envia, sede, "
            + "etiquetas y fecha_referencia se ignoran y se responde la ficha unica.")]
        string? identificacion,
        [McpToolProperty("sede", "Codigo de la sede para ver solo sus colaboradores vinculados.")]
        string? sede,
        [McpToolProperty(
            "etiquetas",
            "Pares categoria:valor separados por coma (ej. 'area:tecnologia,turno:diurno'); "
            + "combina todos en AND.")]
        string? etiquetas,
        [McpToolProperty(
            "fecha_referencia",
            "Fecha de vigencia a evaluar, formato yyyy-MM-dd. Si se omite se usa hoy en "
            + "America/Bogota.")]
        string? fechaReferencia,
        CancellationToken ct)
        => throw new NotImplementedException();
}

/// <summary>Contrato de respuesta de listar_colaboradores hacia el asistente (remodelado, issue #530).</summary>
public sealed record CatalogoDeColaboradores(
    int Total,
    int Mostrando,
    string? Nota,
    IReadOnlyList<ColaboradorFicha> Colaboradores);

/// <summary>
/// Colaborador remodelado token-eficiente: sin EtiquetasNormalizadas ni centinela de vigencia
/// abierta (VigenteHasta null = vinculacion abierta), codigoSede tal cual (sin nombre resuelto).
/// </summary>
public sealed record ColaboradorFicha(
    string Identificacion,
    string Nombre,
    string? CodigoSede,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta,
    IReadOnlyList<string> Etiquetas);
