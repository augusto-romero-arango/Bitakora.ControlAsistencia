using Bitakora.ControlAsistencia.Mcp.Asistente.AsignarJornada;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarJornadaPorGrupo;

public partial class AsignarJornadaPorGrupoTool(
    ProgramacionApi programacion, ColaboradoresApi colaboradores, ResolutorSedePorCodigo resolutorSedes,
    ResolutorCandidatosPorGrupo resolutorCandidatos, TimeProvider? reloj = null)
{
    private static readonly TimeZoneInfo ZonaBogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
    private readonly TimeProvider relojDeEjecucion = reloj ?? TimeProvider.System;

    internal const string NombreTool = "asignar_jornada_por_grupo";

    [Function("AsignarJornadaPorGrupo")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Asigna una Jornada a todos los colaboradores vigentes de un grupo, definido por sede y/o "
            + "etiquetas (los criterios se combinan). Sirve para reasignar en bloque antes de desactivar "
            + "una Jornada. Los fallidos se reintentan con asignar_jornada.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("jornada_id", "Identificador de la Jornada, tal como lo da listar_jornadas.", isRequired: true)]
        string jornadaId,
        [McpToolProperty("sede", "Codigo de la sede de trabajo de los colaboradores del grupo.", isRequired: false)]
        string? sede,
        [McpToolProperty("etiquetas", "Pares categoria:valor separados por coma.", isRequired: false)]
        string? etiquetas,
        CancellationToken ct)
    {
        var mensajesSelector = new MensajesDeSelector(
            Mensajes.SelectorObligatorio, Mensajes.EtiquetaMalFormada, Mensajes.SedeDelSelectorNoExiste,
            Mensajes.SedeDelSelectorInactiva, Mensajes.RechazoDelDominio);
        var (selectorInterpretado, rechazoInterpretacion) = SelectorDeGrupo.Interpretar(sede, etiquetas, mensajesSelector);
        if (rechazoInterpretacion is not null)
            return rechazoInterpretacion;

        var (jornada, rechazo) = await AsignadorDeJornada.ValidarJornadaAsync(programacion, jornadaId, ct);
        if (rechazo is not null)
            return rechazo;

        var (selector, rechazoSelector) = await selectorInterpretado!.ValidarSedeAsync(resolutorSedes, mensajesSelector, ct);
        if (rechazoSelector is not null)
            return rechazoSelector;

        var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(relojDeEjecucion.GetUtcNow(), ZonaBogota).DateTime);
        var resolucion = await resolutorCandidatos.ResolverAsync(hoy, selector!.CodigoSede, selector.Filtros, ct);
        if (resolucion.FalloDeLectura is { } fallo)
            return string.Format(Mensajes.RechazoDelDominio, fallo);
        if (resolucion.Candidatos.Count == 0)
            return string.Format(Mensajes.GrupoSinColaboradores, selector.Descripcion);

        var ejecucion = await AsignadorDeJornada.AsignarAsync(colaboradores, resolucion.Candidatos, jornada!, ct);

        return RespuestaJson.Serializar(new JornadaAsignadaAColaboradoresResumen(
            AsignarJornadaTool.Mensajes.ResultadoJornadaAsignada,
            jornada!.Describir().Resumir(),
            selector.Descripcion,
            ejecucion.Asignados,
            ejecucion.YaLaTenian,
            null,
            ejecucion.Fallidos.Count == 0 ? null : ejecucion.Fallidos,
            AsignarJornadaTool.Mensajes.NotaVisibilidadEventual));
    }
}
