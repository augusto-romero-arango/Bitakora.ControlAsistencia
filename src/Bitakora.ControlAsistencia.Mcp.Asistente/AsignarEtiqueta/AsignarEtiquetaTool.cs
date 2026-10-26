using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarEtiqueta;

public partial class AsignarEtiquetaTool(
    ColaboradoresApi colaboradores, ResolutorCandidatosPorLista resolutorCandidatos)
{
    internal const string NombreTool = "asignar_etiqueta";

    [Function("AsignarEtiqueta")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Asigna una etiqueta (categoria y valor) a colaboradores concretos; si ya tenian un valor en esa categoria, lo reemplaza. Recibe identificaciones completas, tal como las devuelve buscar_colaboradores.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("categoria", "Categoria de la etiqueta, por ejemplo 'area'.", isRequired: true)]
        string categoria,
        [McpToolProperty("valor", "Valor de la etiqueta en esa categoria, por ejemplo 'SERVICIO AL USUARIO'.", isRequired: true)]
        string valor,
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
        if (string.IsNullOrWhiteSpace(valor))
            return string.Format(Mensajes.CampoObligatorio, "valor");
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
            (identificacion, token) => colaboradores.AsignarEtiqueta(identificacion, categoria, valor, token),
            ct);

        return RespuestaJson.Serializar(new EtiquetaAsignadaResumen(
            Mensajes.ResultadoEtiquetaAsignada,
            $"{categoria}:{valor}",
            ejecucion.Exitosos,
            noEncontrados.Count == 0 ? null : noEncontrados,
            ejecucion.Fallidos.Count == 0 ? null : ejecucion.Fallidos,
            Mensajes.NotaVisibilidadEventual));
    }
}
