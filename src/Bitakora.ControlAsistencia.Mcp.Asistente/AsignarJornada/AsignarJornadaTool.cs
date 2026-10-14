using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarJornada;

public partial class AsignarJornadaTool(
    ProgramacionApi programacion, ColaboradoresApi colaboradores, ResolutorCandidatosPorLista resolutorCandidatos)
{
    internal const string NombreTool = "asignar_jornada";

    [Function("AsignarJornada")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Asigna una Jornada a colaboradores concretos: es la Jornada contra la que se estampa y se "
            + "verifica su programacion. Recibe identificaciones completas, tal como las devuelve "
            + "buscar_colaboradores. Para un grupo por sede o etiquetas usa asignar_jornada_por_grupo.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("jornada_id", "Identificador de la Jornada, tal como lo da listar_jornadas.", isRequired: true)]
        string jornadaId,
        [McpToolProperty(
            "identificaciones",
            "Identificaciones completas separadas por coma ('CC-79879078, CE-887766'), tal como "
            + "las devuelve buscar_colaboradores.",
            isRequired: true)]
        string identificaciones,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(identificaciones))
            return string.Format(Mensajes.CampoObligatorio, "identificaciones");

        var (jornada, rechazo) = await AsignadorDeJornada.ValidarJornadaAsync(programacion, jornadaId, ct);
        if (rechazo is not null)
            return rechazo;

        var solicitadas = identificaciones
            .Split(',')
            .Select(i => i.Trim())
            .Where(i => i.Length > 0)
            .ToList();
        if (solicitadas.Count == 0)
            return string.Format(Mensajes.CampoObligatorio, "identificaciones");

        var resolucion = await resolutorCandidatos.ResolverAsync(solicitadas, ct);
        if (resolucion.FalloDeLectura is { } fallo)
            return string.Format(Mensajes.RechazoDelDominio, fallo);

        var encontradas = resolucion.Candidatos
            .Select(c => c.Identificacion.Trim().ToUpperInvariant())
            .ToHashSet();
        var noEncontrados = solicitadas
            .Where(i => !encontradas.Contains(i.ToUpperInvariant()))
            .ToList();

        var ejecucion = await AsignadorDeJornada.AsignarAsync(colaboradores, resolucion.Candidatos, jornada!, ct);

        return RespuestaJson.Serializar(new JornadaAsignadaAColaboradoresResumen(
            Mensajes.ResultadoJornadaAsignada,
            jornada!.Describir().Resumir(),
            null,
            ejecucion.Asignados,
            ejecucion.YaLaTenian,
            noEncontrados.Count == 0 ? null : noEncontrados,
            ejecucion.Fallidos.Count == 0 ? null : ejecucion.Fallidos,
            Mensajes.NotaVisibilidadEventual));
    }
}
