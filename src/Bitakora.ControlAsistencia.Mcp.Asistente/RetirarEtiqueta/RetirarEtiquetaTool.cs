using System.Text.Json.Serialization;
using Bitakora.ControlAsistencia.Mcp.Asistente.AsignarEtiqueta;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.RetirarEtiqueta;

public partial class RetirarEtiquetaTool(
    ColaboradoresApi colaboradores, ResolutorCandidatosPorLista resolutorCandidatos)
{
    internal const string NombreTool = "retirar_etiqueta";

    [Function("RetirarEtiqueta")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Retira la etiqueta de una categoria a colaboradores concretos. Recibe identificaciones completas, tal como las devuelve buscar_colaboradores.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": true, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("categoria", "Categoria de la etiqueta, por ejemplo 'area'.", isRequired: true)]
        string categoria,
        [McpToolProperty(
            "identificaciones",
            "Identificaciones completas separadas por coma ('CC-79879078, CE-887766'), tal como "
            + "las devuelve buscar_colaboradores.",
            isRequired: true)]
        string identificaciones,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(categoria))
            return string.Format(Mensajes.CampoObligatorio, "categoria");
        if (string.IsNullOrWhiteSpace(identificaciones))
            return string.Format(Mensajes.CampoObligatorio, "identificaciones");

        var solicitadas = AplicadorDeEtiqueta.SepararIdentificaciones(identificaciones);
        if (solicitadas.Count == 0)
            return string.Format(Mensajes.CampoObligatorio, "identificaciones");

        var resolucion = await resolutorCandidatos.ResolverAsync(solicitadas, ct);
        if (resolucion.FalloDeLectura is { } fallo)
            return string.Format(Mensajes.RechazoDelDominio, fallo);

        var noEncontrados = AplicadorDeEtiqueta.NoEncontradas(solicitadas, resolucion.Candidatos);
        var ejecucion = await AplicadorDeEtiqueta.AplicarAsync(
            resolucion.Candidatos,
            (identificacion, token) => colaboradores.RetirarEtiqueta(identificacion, categoria, token),
            ct);

        return RespuestaJson.Serializar(new EtiquetaRetiradaResumen(
            Mensajes.ResultadoEtiquetaRetirada,
            categoria,
            ejecucion.Exitosos,
            noEncontrados.Count == 0 ? null : noEncontrados,
            ejecucion.Fallidos.Count == 0 ? null : ejecucion.Fallidos,
            Mensajes.NotaVisibilidadEventual));
    }
}

public sealed record EtiquetaRetiradaResumen(
    string Resultado,
    string Etiqueta,
    int Retirados,
    [property: JsonPropertyName("no_encontrados")] IReadOnlyList<string>? NoEncontrados,
    IReadOnlyList<EtiquetaFallida>? Fallidos,
    string Nota);
